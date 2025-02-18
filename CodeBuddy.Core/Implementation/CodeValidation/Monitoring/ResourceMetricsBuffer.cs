using System;
using System.Collections.Concurrent;
using System.Linq;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ResourceMetricsBuffer
    {
        private readonly ConcurrentQueue<(DateTime Timestamp, ResourceMetrics Metrics)> _metricsHistory;
        private readonly ConcurrentDictionary<string, MiddlewareMetrics> _middlewareMetrics;
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        private readonly object _lockObject = new object();

        public ResourceMetricsBuffer()
        {
            _metricsHistory = new ConcurrentQueue<(DateTime, ResourceMetrics)>();
            _middlewareMetrics = new ConcurrentDictionary<string, MiddlewareMetrics>();
        }

        public void AddMetrics(ResourceMetrics metrics)
        {
            _metricsHistory.Enqueue((DateTime.UtcNow, metrics));
            CleanupOldMetrics();
        }

        public void AddMiddlewareExecution(string middlewareName, bool success, TimeSpan duration)
        {
            _middlewareMetrics.AddOrUpdate(
                middlewareName,
                _ => new MiddlewareMetrics { LastExecutionTime = DateTime.UtcNow, LastDuration = duration, IsSuccessful = success },
                (_, existing) =>
                {
                    existing.LastExecutionTime = DateTime.UtcNow;
                    existing.LastDuration = duration;
                    existing.IsSuccessful = success;
                    return existing;
                });
        }

        public void AddRetryAttempt(string middlewareName)
        {
            _middlewareMetrics.AddOrUpdate(
                middlewareName,
                _ => new MiddlewareMetrics { RetryCount = 1 },
                (_, existing) =>
                {
                    existing.RetryCount++;
                    return existing;
                });
        }

        public void UpdateCircuitBreakerStatus(string middlewareName, bool isOpen)
        {
            _middlewareMetrics.AddOrUpdate(
                middlewareName,
                _ => new MiddlewareMetrics { CircuitBreakerOpen = isOpen },
                (_, existing) =>
                {
                    existing.CircuitBreakerOpen = isOpen;
                    return existing;
                });
        }

        public ResourceMetrics GetCurrentMetrics()
        {
            if (_metricsHistory.TryPeek(out var latest))
            {
                return latest.Metrics;
            }

            return new ResourceMetrics();
        }

        public double GetPeakCpuUsage()
        {
            return _metricsHistory.Max(m => m.Metrics.CpuUsagePercent);
        }

        public double GetAverageCpuUsage()
        {
            return _metricsHistory.Average(m => m.Metrics.CpuUsagePercent);
        }

        public TrendDirection GetCpuUsageTrend()
        {
            return CalculateTrend(m => m.Metrics.CpuUsagePercent);
        }

        public double GetPeakMemoryUsage()
        {
            return _metricsHistory.Max(m => m.Metrics.MemoryUsageMB);
        }

        public double GetAverageMemoryUsage()
        {
            return _metricsHistory.Average(m => m.Metrics.MemoryUsageMB);
        }

        public TrendDirection GetMemoryUsageTrend()
        {
            return CalculateTrend(m => m.Metrics.MemoryUsageMB);
        }

        public double GetPeakDiskUtilization()
        {
            return _metricsHistory.Max(m => m.Metrics.DiskIoMBPS);
        }

        public double GetAverageDiskUtilization()
        {
            return _metricsHistory.Average(m => m.Metrics.DiskIoMBPS);
        }

        public TrendDirection GetDiskUtilizationTrend()
        {
            return CalculateTrend(m => m.Metrics.DiskIoMBPS);
        }

        public double GetPeakNetworkUtilization()
        {
            return _metricsHistory.Max(m => m.Metrics.NetworkBandwidthUsage);
        }

        public double GetAverageNetworkUtilization()
        {
            return _metricsHistory.Average(m => m.Metrics.NetworkBandwidthUsage);
        }

        public TrendDirection GetNetworkUtilizationTrend()
        {
            return CalculateTrend(m => m.Metrics.NetworkBandwidthUsage);
        }

        public int GetConcurrentOperations()
        {
            return _middlewareMetrics.Count(m => 
                (DateTime.UtcNow - m.Value.LastExecutionTime) <= TimeSpan.FromSeconds(30));
        }

        private void CleanupOldMetrics()
        {
            var cutoff = DateTime.UtcNow - _retentionPeriod;
            while (_metricsHistory.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
            {
                _metricsHistory.TryDequeue(out _);
            }
        }

        private TrendDirection CalculateTrend<T>(Func<(DateTime Timestamp, ResourceMetrics Metrics), T> selector)
            where T : IComparable<T>
        {
            var recentMetrics = _metricsHistory
                .Where(m => (DateTime.UtcNow - m.Timestamp) <= TimeSpan.FromMinutes(5))
                .OrderBy(m => m.Timestamp)
                .ToList();

            if (recentMetrics.Count < 2)
                return TrendDirection.Stable;

            var first = Convert.ToDouble(selector(recentMetrics.First()));
            var last = Convert.ToDouble(selector(recentMetrics.Last()));
            var difference = last - first;
            var threshold = Math.Abs(first) * 0.05; // 5% change threshold

            if (Math.Abs(difference) <= threshold)
                return TrendDirection.Stable;

            return difference > 0 ? TrendDirection.Increasing : TrendDirection.Decreasing;
        }

        private class MiddlewareMetrics
        {
            public DateTime LastExecutionTime { get; set; }
            public TimeSpan LastDuration { get; set; }
            public bool IsSuccessful { get; set; }
            public int RetryCount { get; set; }
            public bool CircuitBreakerOpen { get; set; }
        }
    }
}