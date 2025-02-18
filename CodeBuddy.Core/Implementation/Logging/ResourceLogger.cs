using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Logging;
using System.Text.Json;

namespace CodeBuddy.Core.Implementation.Logging
{
    public class ResourceLogger : IDisposable
    {
        private readonly ResourceLoggingConfig _config;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly StreamWriter _logFileWriter;
        private readonly StreamWriter _metricsFileWriter;
        private bool _disposed;

        public ResourceLogger(ResourceLoggingConfig config)
        {
            _config = config;
            _logQueue = new ConcurrentQueue<LogEntry>();

            if (config.EnableFileLogging)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(config.LogFilePath));
                _logFileWriter = new StreamWriter(config.LogFilePath, true);
            }

            if (config.EnableMetricsLogging)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(config.MetricsFilePath));
                _metricsFileWriter = new StreamWriter(config.MetricsFilePath, true);
            }

            StartLogProcessor();
        }

        public void Log(LogLevel level, string resourceName, string operationType, string message, 
            Dictionary<string, object> metadata = null)
        {
            if (level < _config.MinimumLogLevel)
                return;

            if (_config.ResourceSpecificLogLevels.TryGetValue(resourceName, out var resourceLevel) 
                && level < resourceLevel)
                return;

            var entry = new LogEntry
            {
                Level = level,
                ResourceName = resourceName,
                OperationType = operationType,
                Message = message,
                Metadata = metadata,
                Timestamp = DateTime.UtcNow.ToString("o"),
                CorrelationId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString()
            };

            _logQueue.Enqueue(entry);

            if (_config.EnableConsoleLogging)
                WriteToConsole(entry);
        }

        private void StartLogProcessor()
        {
            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    while (_logQueue.TryDequeue(out var entry))
                    {
                        if (_config.EnableFileLogging)
                            await WriteToFileAsync(entry);
                        
                        if (_config.EnableMetricsLogging && entry.Metadata?.Count > 0)
                            await WriteMetricsAsync(entry);
                    }
                    await Task.Delay(100);
                }
            });
        }

        private async Task WriteToFileAsync(LogEntry entry)
        {
            var json = JsonSerializer.Serialize(entry);
            await _logFileWriter.WriteLineAsync(json);
            await _logFileWriter.FlushAsync();
        }

        private async Task WriteMetricsAsync(LogEntry entry)
        {
            var json = JsonSerializer.Serialize(new
            {
                entry.Timestamp,
                entry.ResourceName,
                entry.OperationType,
                Metrics = entry.Metadata
            });
            await _metricsFileWriter.WriteLineAsync(json);
            await _metricsFileWriter.FlushAsync();
        }

        private void WriteToConsole(LogEntry entry)
        {
            var color = entry.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{entry.Timestamp}] [{entry.Level}] [{entry.ResourceName}] [{entry.OperationType}] {entry.Message}");
            Console.ForegroundColor = originalColor;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _logFileWriter?.Dispose();
            _metricsFileWriter?.Dispose();
        }
    }
}