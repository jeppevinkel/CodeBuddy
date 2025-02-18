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
        Task PublishMetricsAsync(string nodeId, ResourceMetrics metrics);
        MetricsSummary GetCurrentMetrics();
        IEnumerable<MetricsSummary> GetHistoricalMetrics(TimeSpan timeWindow);
        Task<ClusterMetrics> GetClusterMetricsAsync();
        Task<Dictionary<string, ResourceMetrics>> GetNodeMetricsAsync();
    }

    public class MetricsAggregator : IMetricsAggregator
    {
        private readonly ConcurrentDictionary<string, MiddlewareMetrics> _middlewareMetrics = new();
        private readonly ConcurrentQueue<TimestampedMetrics> _historicalMetrics = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TimestampedNodeMetrics>> _nodeMetrics = new();
        private readonly Timer _aggregationTimer;
        private readonly ValidationResilienceConfig _config;
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

        public async Task PublishMetricsAsync(string nodeId, ResourceMetrics metrics)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentNullException(nameof(nodeId));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            var nodeQueue = _nodeMetrics.GetOrAdd(nodeId, _ => new ConcurrentQueue<TimestampedNodeMetrics>());
            nodeQueue.Enqueue(new TimestampedNodeMetrics
            {
                NodeId = nodeId,
                Metrics = metrics,
                Timestamp = DateTime.UtcNow
            });

            // Prune old metrics
            while (nodeQueue.TryPeek(out var oldMetrics) && 
                   oldMetrics.Timestamp < DateTime.UtcNow - TimeSpan.FromHours(MetricsRetentionHours))
            {
                nodeQueue.TryDequeue(out _);
            }

            await Task.CompletedTask;
        }

        public async Task<ClusterMetrics> GetClusterMetricsAsync()
        {
            var nodeMetrics = new Dictionary<string, ResourceMetrics>();
            var aggregatedMetrics = new ResourceMetrics();
            var activeNodes = 0;

            foreach (var node in _nodeMetrics)
            {
                var latestMetrics = node.Value.LastOrDefault();
                if (latestMetrics != null && 
                    latestMetrics.Timestamp > DateTime.UtcNow - _config.NodeHealthCheckInterval)
                {
                    nodeMetrics[node.Key] = latestMetrics.Metrics;
                    aggregatedMetrics.CpuUsagePercent += latestMetrics.Metrics.CpuUsagePercent;
                    aggregatedMetrics.MemoryUsageMB += latestMetrics.Metrics.MemoryUsageMB;
                    aggregatedMetrics.DiskIoMBPS += latestMetrics.Metrics.DiskIoMBPS;
                    aggregatedMetrics.ActiveThreads += latestMetrics.Metrics.ActiveThreads;
                    activeNodes++;
                }
            }

            if (activeNodes > 0)
            {
                aggregatedMetrics.CpuUsagePercent /= activeNodes;
                aggregatedMetrics.MemoryUsageMB /= activeNodes;
                aggregatedMetrics.DiskIoMBPS /= activeNodes;
            }

            return await Task.FromResult(new ClusterMetrics
            {
                NodeMetrics = nodeMetrics,
                AggregatedMetrics = aggregatedMetrics,
                ActiveNodes = activeNodes,
                TotalNodes = _nodeMetrics.Count,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task<Dictionary<string, ResourceMetrics>> GetNodeMetricsAsync()
        {
            var metrics = new Dictionary<string, ResourceMetrics>();
            
            foreach (var node in _nodeMetrics)
            {
                var latestMetrics = node.Value.LastOrDefault();
                if (latestMetrics != null && 
                    latestMetrics.Timestamp > DateTime.UtcNow - _config.NodeHealthCheckInterval)
                {
                    metrics[node.Key] = latestMetrics.Metrics;
                }
            }

            return await Task.FromResult(metrics);
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
        public double CpuUsage => CpuUsagePercent;
        public double MemoryUsage => MemoryUsageMB;
        public double DiskIoUsage => DiskIoMBPS;
    }

    public class ClusterMetrics
    {
        public Dictionary<string, ResourceMetrics> NodeMetrics { get; set; }
        public ResourceMetrics AggregatedMetrics { get; set; }
        public int ActiveNodes { get; set; }
        public int TotalNodes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TimestampedNodeMetrics
    {
        public string NodeId { get; set; }
        public ResourceMetrics Metrics { get; set; }
        public DateTime Timestamp { get; set; }
    }
}