using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsAggregator
    {
        void RecordMiddlewareExecution(string middleware, bool success, TimeSpan duration);
        void RecordCircuitBreakerStatus(string middleware, bool isOpen);
        void RecordRetryAttempt(string middleware);
        void RecordResourceUtilization(ResourceMetrics metrics);
        MetricsSummary GetCurrentMetrics();
        IEnumerable<MetricsSummary> GetHistoricalMetrics(TimeSpan timeWindow);
    }

    public class MetricsAggregator : IMetricsAggregator
    {
        private readonly ConcurrentDictionary<string, MiddlewareMetrics> _middlewareMetrics = new();
        private readonly ConcurrentQueue<TimestampedMetrics> _historicalMetrics = new();
        private readonly Timer _aggregationTimer;
        private const int MetricsRetentionHours = 24;

        public MetricsAggregator()
        {
            _aggregationTimer = new Timer(PruneHistoricalMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        public void RecordMiddlewareExecution(string middleware, bool success, TimeSpan duration)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middleware, _ => new MiddlewareMetrics());
            if (success)
            {
                Interlocked.Increment(ref metrics.SuccessCount);
            }
            else
            {
                Interlocked.Increment(ref metrics.FailureCount);
            }
            metrics.AddExecutionTime(duration);
        }

        public void RecordCircuitBreakerStatus(string middleware, bool isOpen)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middleware, _ => new MiddlewareMetrics());
            metrics.UpdateCircuitBreakerStatus(isOpen);
        }

        public void RecordRetryAttempt(string middleware)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middleware, _ => new MiddlewareMetrics());
            Interlocked.Increment(ref metrics.RetryCount);
        }

        public void RecordResourceUtilization(ResourceMetrics metrics)
        {
            var timestamp = DateTime.UtcNow;
            _historicalMetrics.Enqueue(new TimestampedMetrics
            {
                Timestamp = timestamp,
                Metrics = new MetricsSummary
                {
                    ResourceMetrics = metrics,
                    MiddlewareMetrics = GetCurrentMiddlewareMetrics()
                }
            });
        }

        public MetricsSummary GetCurrentMetrics()
        {
            return new MetricsSummary
            {
                MiddlewareMetrics = GetCurrentMiddlewareMetrics(),
                ResourceMetrics = GetLatestResourceMetrics()
            };
        }

        public IEnumerable<MetricsSummary> GetHistoricalMetrics(TimeSpan timeWindow)
        {
            var cutoff = DateTime.UtcNow - timeWindow;
            return _historicalMetrics
                .Where(m => m.Timestamp >= cutoff)
                .Select(m => m.Metrics);
        }

        private void PruneHistoricalMetrics(object state)
        {
            var cutoff = DateTime.UtcNow.AddHours(-MetricsRetentionHours);
            while (_historicalMetrics.TryPeek(out var metrics) && metrics.Timestamp < cutoff)
            {
                _historicalMetrics.TryDequeue(out _);
            }
        }

        private Dictionary<string, MiddlewareMetrics> GetCurrentMiddlewareMetrics()
        {
            return _middlewareMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private ResourceMetrics GetLatestResourceMetrics()
        {
            return _historicalMetrics.LastOrDefault()?.Metrics.ResourceMetrics ?? new ResourceMetrics();
        }

        private class TimestampedMetrics
        {
            public DateTime Timestamp { get; set; }
            public MetricsSummary Metrics { get; set; }
        }
    }

    public class MiddlewareMetrics
    {
        public long SuccessCount;
        public long FailureCount;
        public long RetryCount;
        private bool _circuitBreakerOpen;
        private readonly ConcurrentQueue<double> _executionTimes = new();
        private const int MaxStoredExecutionTimes = 1000;

        public void AddExecutionTime(TimeSpan duration)
        {
            _executionTimes.Enqueue(duration.TotalMilliseconds);
            while (_executionTimes.Count > MaxStoredExecutionTimes)
            {
                _executionTimes.TryDequeue(out _);
            }
        }

        public void UpdateCircuitBreakerStatus(bool isOpen)
        {
            _circuitBreakerOpen = isOpen;
        }

        public double AverageExecutionTime => _executionTimes.Any() ? _executionTimes.Average() : 0;
        public bool CircuitBreakerOpen => _circuitBreakerOpen;
    }

    public class MetricsSummary
    {
        public Dictionary<string, MiddlewareMetrics> MiddlewareMetrics { get; set; }
        public ResourceMetrics ResourceMetrics { get; set; }
    }

    public class ResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskIoMBPS { get; set; }
        public int ActiveThreads { get; set; }
    }
}