using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Logging;
using CodeBuddy.Core.Models.Analytics;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Core.Implementation.CodeValidation.Logging
{
    public interface IResourceLoggingService
    {
        void LogResourceAllocation(string resourceName, string operationType, Dictionary<string, object> metrics, string componentName = null);
        void LogResourceDeallocation(string resourceName, string operationType, Dictionary<string, object> metrics, string componentName = null);
        void LogResourceWarning(string resourceName, string message, Dictionary<string, object> context, string componentName = null);
        void LogResourceError(string resourceName, string message, Exception ex, string componentName = null);
        string CreateCorrelationId();
        Task<IEnumerable<LogEntry>> GetResourceLogs(string resourceName, DateTime startTime, DateTime endTime);
        Task<Dictionary<string, object>> GetResourceMetrics(string resourceName);
        Task<ResourceHealthReport> GenerateResourceHealthReport();
        Task<IEnumerable<LogEntry>> GetRelatedOperations(string correlationId);
        Task<ResourceUsageStatistics> CalculateResourceUsageStatistics(string resourceName, TimeSpan period);
    }

    public class ResourceLoggingService : IResourceLoggingService
    {
        private readonly ResourceLoggingConfig _config;
        private readonly ConcurrentDictionary<string, List<LogEntry>> _inMemoryLogs;
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _resourceMetrics;
        private readonly ConcurrentDictionary<string, List<ResourceUsageStatistics>> _usageStatistics;
        private long _correlationCounter;

        public ResourceLoggingService(ResourceLoggingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _inMemoryLogs = new ConcurrentDictionary<string, List<LogEntry>>();
            _resourceMetrics = new ConcurrentDictionary<string, Dictionary<string, object>>();
            _usageStatistics = new ConcurrentDictionary<string, List<ResourceUsageStatistics>>();
            _correlationCounter = 0;
        }

        public void LogResourceAllocation(string resourceName, string operationType, Dictionary<string, object> metrics, string componentName = null)
        {
            var entry = CreateLogEntry(LogLevel.Information, resourceName, $"Resource allocated: {operationType}", 
                operationType, metrics);
            LogEntry(entry);
            UpdateResourceMetrics(resourceName, metrics);
        }

        public void LogResourceDeallocation(string resourceName, string operationType, Dictionary<string, object> metrics, string componentName = null)
        {
            var entry = CreateLogEntry(LogLevel.Information, resourceName, $"Resource deallocated: {operationType}", 
                operationType, metrics);
            LogEntry(entry);
            UpdateResourceMetrics(resourceName, metrics);
        }

        public void LogResourceWarning(string resourceName, string message, Dictionary<string, object> context, string componentName = null)
        {
            var entry = CreateLogEntry(LogLevel.Warning, resourceName, message, "Warning", context);
            LogEntry(entry);
        }

        public void LogResourceError(string resourceName, string message, Exception ex, string componentName = null)
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

        public async Task<ResourceHealthReport> GenerateResourceHealthReport()
        {
            var report = new ResourceHealthReport
            {
                GeneratedAt = DateTime.UtcNow,
                ResourceMetrics = new Dictionary<string, ResourceHealthMetrics>(),
                Alerts = new List<ResourceAlert>(),
                UsageStatistics = new Dictionary<string, ResourceUsageStatistics>(),
                Trends = new List<ResourceTrend>()
            };

            foreach (var resourceName in _inMemoryLogs.Keys)
            {
                var logs = await GetResourceLogs(resourceName, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
                var stats = await CalculateResourceUsageStatistics(resourceName, TimeSpan.FromDays(1));
                
                report.ResourceMetrics[resourceName] = new ResourceHealthMetrics
                {
                    ResourceName = resourceName,
                    CurrentAllocationCount = stats.TotalAllocations - stats.TotalDeallocations,
                    PeakAllocationCount = stats.TotalAllocations,
                    AverageUsage = stats.AverageUsagePercent,
                    AverageLifetime = stats.AverageAllocationDuration,
                    ErrorCount = logs.Count(l => l.Level == LogLevel.Error),
                    WarningCount = logs.Count(l => l.Level == LogLevel.Warning),
                    HealthScore = CalculateHealthScore(logs, stats)
                };

                report.UsageStatistics[resourceName] = stats;

                // Add alerts for concerning patterns
                if (stats.OrphanedResourceCount > 0)
                {
                    report.Alerts.Add(new ResourceAlert
                    {
                        ResourceName = resourceName,
                        AlertType = "OrphanedResources",
                        Message = $"Found {stats.OrphanedResourceCount} orphaned resources",
                        DetectedAt = DateTime.UtcNow,
                        Context = new Dictionary<string, object> { 
                            { "OrphanCount", stats.OrphanedResourceCount }
                        }
                    });
                }

                // Add usage trends
                report.Trends.Add(new ResourceTrend
                {
                    ResourceName = resourceName,
                    TrendType = "Usage",
                    TrendValue = CalculateUsageTrend(logs),
                    Analysis = GenerateTrendAnalysis(logs),
                    TrendMetrics = new Dictionary<string, object>
                    {
                        { "PeakUsage", stats.TotalAllocations },
                        { "AverageUsage", stats.AverageUsagePercent }
                    }
                });
            }

            return report;
        }

        public async Task<IEnumerable<LogEntry>> GetRelatedOperations(string correlationId)
        {
            var relatedLogs = new List<LogEntry>();
            foreach (var logs in _inMemoryLogs.Values)
            {
                relatedLogs.AddRange(logs.Where(l => l.CorrelationId == correlationId));
            }
            return relatedLogs.OrderBy(l => l.Timestamp);
        }

        public async Task<ResourceUsageStatistics> CalculateResourceUsageStatistics(string resourceName, TimeSpan period)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - period;
            var logs = await GetResourceLogs(resourceName, startTime, endTime);

            var stats = new ResourceUsageStatistics
            {
                ResourceName = resourceName,
                PeriodStart = startTime,
                PeriodEnd = endTime,
                TotalAllocations = logs.Count(l => l.OperationType.Contains("Allocation")),
                TotalDeallocations = logs.Count(l => l.OperationType.Contains("Deallocation")),
                OrphanedResourceCount = logs.Count(l => l.OperationType.Contains("Orphaned")),
                ComponentUsageDistribution = logs
                    .GroupBy(l => l.ComponentName)
                    .ToDictionary(g => g.Key, g => (double)g.Count() / logs.Count)
            };

            // Calculate average allocation duration
            var allocationTimes = new Dictionary<string, DateTime>();
            TimeSpan totalDuration = TimeSpan.Zero;
            int completedAllocations = 0;

            foreach (var log in logs.OrderBy(l => l.Timestamp))
            {
                if (log.OperationType.Contains("Allocation"))
                {
                    allocationTimes[log.CorrelationId] = DateTime.Parse(log.Timestamp);
                }
                else if (log.OperationType.Contains("Deallocation") && allocationTimes.ContainsKey(log.CorrelationId))
                {
                    var duration = DateTime.Parse(log.Timestamp) - allocationTimes[log.CorrelationId];
                    totalDuration += duration;
                    completedAllocations++;
                    allocationTimes.Remove(log.CorrelationId);
                }
            }

            stats.AverageAllocationDuration = completedAllocations > 0 
                ? TimeSpan.FromTicks(totalDuration.Ticks / completedAllocations)
                : TimeSpan.Zero;

            // Calculate average usage percentage
            stats.AverageUsagePercent = period.TotalSeconds > 0 
                ? (totalDuration.TotalSeconds / (period.TotalSeconds * stats.TotalAllocations)) * 100
                : 0;

            return stats;
        }

        private double CalculateHealthScore(IEnumerable<LogEntry> logs, ResourceUsageStatistics stats)
        {
            double score = 100;

            // Deduct points for errors and warnings
            score -= logs.Count(l => l.Level == LogLevel.Error) * 10;
            score -= logs.Count(l => l.Level == LogLevel.Warning) * 5;

            // Deduct points for orphaned resources
            score -= stats.OrphanedResourceCount * 8;

            // Deduct points for high average usage
            if (stats.AverageUsagePercent > 90) score -= 10;
            else if (stats.AverageUsagePercent > 80) score -= 5;

            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateUsageTrend(IEnumerable<LogEntry> logs)
        {
            var timeOrderedLogs = logs.OrderBy(l => l.Timestamp).ToList();
            if (timeOrderedLogs.Count < 2) return 0;

            var firstTimestamp = DateTime.Parse(timeOrderedLogs.First().Timestamp);
            var lastTimestamp = DateTime.Parse(timeOrderedLogs.Last().Timestamp);
            var totalDuration = (lastTimestamp - firstTimestamp).TotalHours;

            if (totalDuration == 0) return 0;

            var allocationsPerHour = timeOrderedLogs.Count / totalDuration;
            return allocationsPerHour;
        }

        private string GenerateTrendAnalysis(IEnumerable<LogEntry> logs)
        {
            var trend = CalculateUsageTrend(logs);
            var errorRate = (double)logs.Count(l => l.Level == LogLevel.Error) / logs.Count();

            if (trend > 100 && errorRate < 0.1)
                return "High usage with good stability";
            else if (trend > 100)
                return "High usage with concerning error rate";
            else if (trend < 10 && errorRate > 0.2)
                return "Low usage with high error rate - potential issues";
            else if (trend < 10)
                return "Low usage - potentially underutilized";
            else
                return "Moderate usage with normal patterns";
        }

        private LogEntry CreateLogEntry(LogLevel level, string resourceName, string message, 
            string operationType, Dictionary<string, object> metadata, string componentName = null)
        {
            return new LogEntry
            {
                Level = level,
                ResourceName = resourceName,
                Message = message,
                OperationType = operationType,
                Metadata = metadata ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow.ToString("O"),
                CorrelationId = CreateCorrelationId(),
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                ComponentName = componentName ?? "UnspecifiedComponent"
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