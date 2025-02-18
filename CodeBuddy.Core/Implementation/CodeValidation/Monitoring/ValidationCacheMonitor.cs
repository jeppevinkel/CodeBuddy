using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.Extensions.Options;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IValidationCacheMonitor
    {
        Task<CachePerformanceMetrics> GetCurrentMetricsAsync();
        Task<List<CachePerformanceMetrics>> GetHistoricalMetricsAsync(DateTime start, DateTime end);
        Task RecordOperationMetricsAsync(string operation, long durationMicroseconds, bool success);
        Task AlertIfThresholdExceededAsync(CachePerformanceMetrics metrics);
        Task StartMonitoringAsync(CancellationToken cancellationToken);
        Task StopMonitoringAsync();
    }

    public class ValidationCacheMonitor : IValidationCacheMonitor
    {
        private readonly IValidationCache _cache;
        private readonly ValidationCacheConfig _config;
        private readonly ConcurrentQueue<CachePerformanceMetrics> _metricsHistory;
        private readonly ConcurrentDictionary<string, (int Count, long TotalTime)> _operationStats;
        private readonly ConcurrentDictionary<string, int> _accessPatterns;
        private readonly object _syncLock = new object();
        private Task _monitoringTask;
        private CancellationTokenSource _cancellationTokenSource;
        
        private const int MAX_HISTORY_ITEMS = 1000;
        private const int MONITORING_INTERVAL_MS = 60000; // 1 minute

        public ValidationCacheMonitor(
            IValidationCache cache,
            IOptions<ValidationCacheConfig> config)
        {
            _cache = cache;
            _config = config.Value;
            _metricsHistory = new ConcurrentQueue<CachePerformanceMetrics>();
            _operationStats = new ConcurrentDictionary<string, (int Count, long TotalTime)>();
            _accessPatterns = new ConcurrentDictionary<string, int>();
        }

        public async Task<CachePerformanceMetrics> GetCurrentMetricsAsync()
        {
            var stats = await _cache.GetStatsAsync();
            var metrics = new CachePerformanceMetrics
            {
                HitRatio = stats.HitRatio,
                MemoryUsageBytes = stats.CacheSizeBytes,
                EvictionCount = stats.InvalidationCount,
                MemoryUtilizationPercentage = (double)stats.CacheSizeBytes / (_config.MaxCacheSizeMB * 1024 * 1024) * 100,
                AverageCachedItemSize = stats.TotalEntries > 0 ? stats.CacheSizeBytes / stats.TotalEntries : 0,
                PartitionCount = GetPartitionCount(),
            };

            // Add operation statistics
            foreach (var op in _operationStats)
            {
                if (op.Value.Count > 0)
                {
                    metrics.AvgLookupLatencyMicroseconds = op.Value.TotalTime / op.Value.Count;
                }
            }

            // Add access patterns
            metrics.AccessPatternFrequency = new Dictionary<string, int>(_accessPatterns);
            metrics.TopAccessedKeys = _accessPatterns
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList();

            return metrics;
        }

        public async Task<List<CachePerformanceMetrics>> GetHistoricalMetricsAsync(DateTime start, DateTime end)
        {
            return _metricsHistory
                .Where(m => m.TimestampUtc >= start && m.TimestampUtc <= end)
                .OrderBy(m => m.TimestampUtc)
                .ToList();
        }

        public async Task RecordOperationMetricsAsync(string operation, long durationMicroseconds, bool success)
        {
            _operationStats.AddOrUpdate(
                operation,
                (1, durationMicroseconds),
                (_, old) => (old.Count + 1, old.TotalTime + durationMicroseconds));

            if (success)
            {
                _accessPatterns.AddOrUpdate(operation, 1, (_, count) => count + 1);
            }
        }

        public async Task AlertIfThresholdExceededAsync(CachePerformanceMetrics metrics)
        {
            var alertThresholds = new Dictionary<string, (Func<CachePerformanceMetrics, bool> Check, string Message)>
            {
                {
                    "HighMemoryUsage",
                    (m => m.MemoryUtilizationPercentage > 90,
                    "Cache memory usage exceeds 90%")
                },
                {
                    "LowHitRatio",
                    (m => m.HitRatio < 0.5,
                    "Cache hit ratio below 50%")
                },
                {
                    "HighLatency",
                    (m => m.AvgLookupLatencyMicroseconds > 1000,
                    "Average lookup latency exceeds 1ms")
                }
            };

            foreach (var threshold in alertThresholds)
            {
                if (threshold.Value.Check(metrics))
                {
                    // TODO: Integrate with your alerting system
                    Console.WriteLine($"ALERT: {threshold.Key} - {threshold.Value.Message}");
                }
            }
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            if (_monitoringTask != null)
            {
                throw new InvalidOperationException("Monitoring is already running");
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _monitoringTask = MonitoringLoopAsync(_cancellationTokenSource.Token);
        }

        public async Task StopMonitoringAsync()
        {
            if (_monitoringTask == null)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            await _monitoringTask;
            _monitoringTask = null;
        }

        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var metrics = await GetCurrentMetricsAsync();
                    
                    _metricsHistory.Enqueue(metrics);
                    while (_metricsHistory.Count > MAX_HISTORY_ITEMS)
                    {
                        _metricsHistory.TryDequeue(out _);
                    }

                    await AlertIfThresholdExceededAsync(metrics);
                    
                    await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // TODO: Log error
                    Console.WriteLine($"Error in monitoring loop: {ex}");
                    await Task.Delay(5000, cancellationToken); // Back off on error
                }
            }
        }

        private int GetPartitionCount()
        {
            // This would be implemented based on your cache partitioning strategy
            return 1; // Default to 1 if no partitioning is implemented
        }
    }
}