using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Logging;

namespace CodeBuddy.Core.Implementation.Logging
{
    public class LoggingService : ILoggingService, IDisposable
    {
        private LoggingConfiguration _config;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processQueueTask;
        private readonly SemaphoreSlim _configLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public LoggingService()
        {
            _config = new LoggingConfiguration();
            _logQueue = new ConcurrentQueue<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();
            _processQueueTask = ProcessQueueAsync(_cancellationTokenSource.Token);
        }

        public void Log(LogEntry entry)
        {
            if (!ShouldLog(entry))
                return;

            if (_config.EnableAsyncLogging)
            {
                _logQueue.Enqueue(entry);
            }
            else
            {
                ProcessLogEntry(entry);
            }
        }

        public async Task LogAsync(LogEntry entry)
        {
            if (!ShouldLog(entry))
                return;

            if (_config.EnableAsyncLogging)
            {
                _logQueue.Enqueue(entry);
            }
            else
            {
                await ProcessLogEntryAsync(entry);
            }
        }

        private bool ShouldLog(LogEntry entry)
        {
            if (_config.ExcludedComponents.Contains(entry.ComponentName))
                return false;

            if (_config.ComponentLogLevels.TryGetValue(entry.ComponentName, out var componentLevel))
            {
                if (entry.Severity < componentLevel)
                    return false;
            }
            else if (entry.Severity < _config.MinimumLogLevel)
            {
                return false;
            }

            if (_config.SamplingRate < 1.0 && Random.Shared.NextDouble() > _config.SamplingRate)
                return false;

            return true;
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_logQueue.TryDequeue(out var entry))
                {
                    await ProcessLogEntryAsync(entry);
                }
                await Task.Delay(100, cancellationToken);
            }
        }

        private void ProcessLogEntry(LogEntry entry)
        {
            var logMessage = FormatLogMessage(entry);

            if (_config.EnableConsoleLogging)
            {
                WriteToConsole(entry.Severity, logMessage);
            }

            if (_config.EnableFileLogging)
            {
                WriteToFile(logMessage);
            }

            if (_config.EnableExternalLogging)
            {
                // Integration point for external logging systems
                WriteToExternalSystem(entry);
            }
        }

        private async Task ProcessLogEntryAsync(LogEntry entry)
        {
            var logMessage = FormatLogMessage(entry);

            if (_config.EnableConsoleLogging)
            {
                WriteToConsole(entry.Severity, logMessage);
            }

            if (_config.EnableFileLogging)
            {
                await WriteToFileAsync(logMessage);
            }

            if (_config.EnableExternalLogging)
            {
                await WriteToExternalSystemAsync(entry);
            }
        }

        private string FormatLogMessage(LogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var message = $"[{timestamp}] [{entry.Severity}] [{entry.ComponentName}] [{entry.CorrelationId}] {entry.Message}";
            
            if (entry.Exception != null)
            {
                message += $"\nException: {entry.Exception}";
            }

            if (entry.AdditionalData != null)
            {
                message += $"\nAdditional Data: {System.Text.Json.JsonSerializer.Serialize(entry.AdditionalData)}";
            }

            return message;
        }

        private void WriteToConsole(LogSeverity severity, string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetColorForSeverity(severity);
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        private ConsoleColor GetColorForSeverity(LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Debug => ConsoleColor.Gray,
                LogSeverity.Info => ConsoleColor.White,
                LogSeverity.Warning => ConsoleColor.Yellow,
                LogSeverity.Error => ConsoleColor.Red,
                LogSeverity.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        private void WriteToFile(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(_config.LogFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_config.LogFilePath, message + Environment.NewLine);
                CheckAndRotateLogFile();
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private async Task WriteToFileAsync(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(_config.LogFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.AppendAllTextAsync(_config.LogFilePath, message + Environment.NewLine);
                await CheckAndRotateLogFileAsync();
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private void CheckAndRotateLogFile()
        {
            try
            {
                var fileInfo = new FileInfo(_config.LogFilePath);
                if (fileInfo.Exists && fileInfo.Length > _config.MaxLogFileSizeBytes)
                {
                    for (int i = _config.MaxLogFileCount - 1; i > 0; i--)
                    {
                        var source = _config.LogFilePath + $".{i}";
                        var destination = _config.LogFilePath + $".{i + 1}";
                        if (File.Exists(source))
                        {
                            if (File.Exists(destination))
                            {
                                File.Delete(destination);
                            }
                            File.Move(source, destination);
                        }
                    }

                    var newFile = _config.LogFilePath + ".1";
                    if (File.Exists(newFile))
                    {
                        File.Delete(newFile);
                    }
                    File.Move(_config.LogFilePath, newFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to rotate log files: {ex.Message}");
            }
        }

        private async Task CheckAndRotateLogFileAsync()
        {
            try
            {
                var fileInfo = new FileInfo(_config.LogFilePath);
                if (fileInfo.Exists && fileInfo.Length > _config.MaxLogFileSizeBytes)
                {
                    await _configLock.WaitAsync();
                    try
                    {
                        for (int i = _config.MaxLogFileCount - 1; i > 0; i--)
                        {
                            var source = _config.LogFilePath + $".{i}";
                            var destination = _config.LogFilePath + $".{i + 1}";
                            if (File.Exists(source))
                            {
                                if (File.Exists(destination))
                                {
                                    File.Delete(destination);
                                }
                                File.Move(source, destination);
                            }
                        }

                        var newFile = _config.LogFilePath + ".1";
                        if (File.Exists(newFile))
                        {
                            File.Delete(newFile);
                        }
                        File.Move(_config.LogFilePath, newFile);
                    }
                    finally
                    {
                        _configLock.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to rotate log files: {ex.Message}");
            }
        }

        private void WriteToExternalSystem(LogEntry entry)
        {
            // Implementation for external logging system integration
            // This could be implemented by derived classes or through a plugin system
        }

        private Task WriteToExternalSystemAsync(LogEntry entry)
        {
            // Async implementation for external logging system integration
            return Task.CompletedTask;
        }

        public void Configure(LoggingConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Flush()
        {
            while (_logQueue.TryDequeue(out var entry))
            {
                ProcessLogEntry(entry);
            }
        }

        public void Debug(string message, string component = null, string correlationId = null, object data = null)
        {
            Log(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Debug,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public void Info(string message, string component = null, string correlationId = null, object data = null)
        {
            Log(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Info,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public void Warning(string message, string component = null, string correlationId = null, object data = null)
        {
            Log(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Warning,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public void Error(string message, Exception ex = null, string component = null, string correlationId = null, object data = null)
        {
            Log(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Error,
                ComponentName = component,
                CorrelationId = correlationId,
                Exception = ex,
                AdditionalData = data
            });
        }

        public void Critical(string message, Exception ex = null, string component = null, string correlationId = null, object data = null)
        {
            Log(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Critical,
                ComponentName = component,
                CorrelationId = correlationId,
                Exception = ex,
                AdditionalData = data
            });
        }

        public Task DebugAsync(string message, string component = null, string correlationId = null, object data = null)
        {
            return LogAsync(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Debug,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public Task InfoAsync(string message, string component = null, string correlationId = null, object data = null)
        {
            return LogAsync(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Info,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public Task WarningAsync(string message, string component = null, string correlationId = null, object data = null)
        {
            return LogAsync(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Warning,
                ComponentName = component,
                CorrelationId = correlationId,
                AdditionalData = data
            });
        }

        public Task ErrorAsync(string message, Exception ex = null, string component = null, string correlationId = null, object data = null)
        {
            return LogAsync(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Error,
                ComponentName = component,
                CorrelationId = correlationId,
                Exception = ex,
                AdditionalData = data
            });
        }

        public Task CriticalAsync(string message, Exception ex = null, string component = null, string correlationId = null, object data = null)
        {
            return LogAsync(new LogEntry
            {
                Message = message,
                Severity = LogSeverity.Critical,
                ComponentName = component,
                CorrelationId = correlationId,
                Exception = ex,
                AdditionalData = data
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Flush();
                _cancellationTokenSource.Cancel();
                try
                {
                    _processQueueTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception) { }
                _cancellationTokenSource.Dispose();
                _configLock.Dispose();
            }

            _disposed = true;
        }
    }
}