using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Logging;
using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.CodeValidation.Logging
{
    public interface IResourceLoggingService
    {
        void LogResourceAllocation(string resourceName, string operationType, Dictionary<string, object> metrics);
        void LogResourceDeallocation(string resourceName, string operationType, Dictionary<string, object> metrics);
        void LogResourceWarning(string resourceName, string message, Dictionary<string, object> context);
        void LogResourceError(string resourceName, string message, Exception ex);
        string CreateCorrelationId();
        Task<IEnumerable<LogEntry>> GetResourceLogs(string resourceName, DateTime startTime, DateTime endTime);
        Task<Dictionary<string, object>> GetResourceMetrics(string resourceName);
    }

    public class ResourceLoggingService : IResourceLoggingService
    {
        private readonly ResourceLoggingConfig _config;
        private readonly ConcurrentDictionary<string, List<LogEntry>> _inMemoryLogs;
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _resourceMetrics;
        private long _correlationCounter;

        public ResourceLoggingService(ResourceLoggingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _inMemoryLogs = new ConcurrentDictionary<string, List<LogEntry>>();
            _resourceMetrics = new ConcurrentDictionary<string, Dictionary<string, object>>();
            _correlationCounter = 0;
        }

        public void LogResourceAllocation(string resourceName, string operationType, Dictionary<string, object> metrics)
        {
            var entry = CreateLogEntry(LogLevel.Information, resourceName, $"Resource allocated: {operationType}", 
                operationType, metrics);
            LogEntry(entry);
            UpdateResourceMetrics(resourceName, metrics);
        }

        public void LogResourceDeallocation(string resourceName, string operationType, Dictionary<string, object> metrics)
        {
            var entry = CreateLogEntry(LogLevel.Information, resourceName, $"Resource deallocated: {operationType}", 
                operationType, metrics);
            LogEntry(entry);
            UpdateResourceMetrics(resourceName, metrics);
        }

        public void LogResourceWarning(string resourceName, string message, Dictionary<string, object> context)
        {
            var entry = CreateLogEntry(LogLevel.Warning, resourceName, message, "Warning", context);
            LogEntry(entry);
        }

        public void LogResourceError(string resourceName, string message, Exception ex)
        {
            var context = new Dictionary<string, object>
            {
                { "ExceptionType", ex.GetType().Name },
                { "ExceptionMessage", ex.Message },
                { "StackTrace", ex.StackTrace }
            };
            var entry = CreateLogEntry(LogLevel.Error, resourceName, message, "Error", context);
            LogEntry(entry);
        }

        public string CreateCorrelationId()
        {
            return $"RES-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _correlationCounter):X8}";
        }

        public async Task<IEnumerable<LogEntry>> GetResourceLogs(string resourceName, DateTime startTime, DateTime endTime)
        {
            if (_inMemoryLogs.TryGetValue(resourceName, out var logs))
            {
                return logs.FindAll(log => 
                    DateTime.Parse(log.Timestamp) >= startTime && 
                    DateTime.Parse(log.Timestamp) <= endTime);
            }
            return Array.Empty<LogEntry>();
        }

        public async Task<Dictionary<string, object>> GetResourceMetrics(string resourceName)
        {
            return _resourceMetrics.GetOrAdd(resourceName, new Dictionary<string, object>());
        }

        private LogEntry CreateLogEntry(LogLevel level, string resourceName, string message, 
            string operationType, Dictionary<string, object> metadata)
        {
            return new LogEntry
            {
                Level = level,
                ResourceName = resourceName,
                Message = message,
                OperationType = operationType,
                Metadata = metadata ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow.ToString("O"),
                CorrelationId = CreateCorrelationId()
            };
        }

        private void LogEntry(LogEntry entry)
        {
            if (ShouldLog(entry))
            {
                var logs = _inMemoryLogs.GetOrAdd(entry.ResourceName, new List<LogEntry>());
                lock (logs)
                {
                    logs.Add(entry);
                    EnforceLogRetention(logs);
                }

                if (_config.EnableFileLogging)
                {
                    WriteToFile(entry);
                }

                if (_config.EnableConsoleLogging)
                {
                    WriteToConsole(entry);
                }
            }
        }

        private bool ShouldLog(LogEntry entry)
        {
            if (_config.ResourceSpecificLogLevels.TryGetValue(entry.ResourceName, out var resourceLevel))
            {
                return entry.Level >= resourceLevel;
            }
            return entry.Level >= _config.MinimumLogLevel;
        }

        private void UpdateResourceMetrics(string resourceName, Dictionary<string, object> metrics)
        {
            if (_config.EnableMetricsLogging && metrics != null)
            {
                var resourceMetrics = _resourceMetrics.GetOrAdd(resourceName, new Dictionary<string, object>());
                lock (resourceMetrics)
                {
                    foreach (var metric in metrics)
                    {
                        resourceMetrics[metric.Key] = metric.Value;
                    }
                }
            }
        }

        private void EnforceLogRetention(List<LogEntry> logs)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_config.LogRetentionDays);
            logs.RemoveAll(log => DateTime.Parse(log.Timestamp) < cutoffDate);
        }

        private void WriteToFile(LogEntry entry)
        {
            // This is a simplified implementation. In production, use a proper file logging system
            // with file rotation, async writing, and error handling
            try
            {
                var logLine = $"{entry.Timestamp}|{entry.Level}|{entry.ResourceName}|{entry.OperationType}|" +
                            $"{entry.CorrelationId}|{entry.Message}";
                System.IO.File.AppendAllLines(_config.LogFilePath, new[] { logLine });
            }
            catch (Exception)
            {
                // Log file writing errors should be handled appropriately in production
            }
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
            Console.WriteLine($"[{entry.Timestamp}] {entry.Level}: {entry.ResourceName} - {entry.Message}");
            Console.ForegroundColor = originalColor;
        }
    }
}