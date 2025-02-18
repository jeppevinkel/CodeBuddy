using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    internal class MetricsCollector : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<double> _latencyBuffer = new();
        private readonly ConcurrentDictionary<string, ValidationInfo> _activeValidations = new();
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _diskCounter;
        private readonly PipelineMetrics _metrics = new();
        private readonly Stopwatch _uptimeStopwatch = new();
        private readonly Timer _metricsUpdateTimer;
        private readonly int _latencyBufferSize = 1000;
        private long _lastTotalRequests;
        private DateTime _lastUpdateTime;
        private bool _disposed;

        public MetricsCollector(ILogger logger)
        {
            _logger = logger;
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _diskCounter = new PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", "_Total", true);
            _uptimeStopwatch.Start();
            _lastUpdateTime = DateTime.UtcNow;
            _metrics.LastResetTime = _lastUpdateTime;

            // Update metrics every second
            _metricsUpdateTimer = new Timer(UpdateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public PipelineMetrics GetCurrentMetrics() => _metrics;

        public void TrackValidationStart(string validationId, int codeSizeBytes)
        {
            _activeValidations.TryAdd(validationId, new ValidationInfo
            {
                StartTime = DateTime.UtcNow,
                CodeSizeBytes = codeSizeBytes,
                Status = ValidationStatus.Running
            });
        }

        public void TrackValidationComplete(string validationId, bool success)
        {
            if (_activeValidations.TryRemove(validationId, out var info))
            {
                var latency = (DateTime.UtcNow - info.StartTime).TotalMilliseconds;
                while (_latencyBuffer.Count >= _latencyBufferSize)
                {
                    _latencyBuffer.TryDequeue(out _);
                }
                _latencyBuffer.Enqueue(latency);

                Interlocked.Increment(ref _metrics.TotalRequestsProcessed);
                if (!success)
                {
                    Interlocked.Increment(ref _metrics.FailedRequests);
                }
            }
        }

        private void UpdateMetrics(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastUpdateTime).TotalSeconds;

                // Update throughput
                var requestDelta = _metrics.TotalRequestsProcessed - _lastTotalRequests;
                _metrics.RequestsPerSecond = requestDelta / elapsed;
                _lastTotalRequests = _metrics.TotalRequestsProcessed;

                // Update latency metrics
                var latencies = _latencyBuffer.ToArray();
                if (latencies.Length > 0)
                {
                    Array.Sort(latencies);
                    _metrics.AverageLatencyMs = latencies.Average();
                    _metrics.P95LatencyMs = latencies[(int)(latencies.Length * 0.95)];
                    _metrics.P99LatencyMs = latencies[(int)(latencies.Length * 0.99)];
                }

                // Update resource utilization
                _metrics.CpuUsagePercent = _cpuCounter.NextValue();
                _metrics.MemoryUsageBytes = Process.GetCurrentProcess().WorkingSet64;
                _metrics.DiskIoMbps = _diskCounter.NextValue() / (1024 * 1024);
                _metrics.ActiveThreads = Process.GetCurrentProcess().Threads.Count;
                _metrics.UptimeTotal = _uptimeStopwatch.Elapsed;

                // Detect stalled validations
                var stalledThreshold = TimeSpan.FromMinutes(5);
                var stalledCount = _activeValidations.Count(v => 
                    v.Value.Status == ValidationStatus.Running && 
                    (now - v.Value.StartTime) > stalledThreshold);
                _metrics.StalledValidations = stalledCount;

                // Detect bottlenecks
                DetectBottlenecks();

                _lastUpdateTime = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics");
            }
        }

        private void DetectBottlenecks()
        {
            // CPU bottleneck
            if (_metrics.CpuUsagePercent > 80)
            {
                _metrics.DetectedBottlenecks.AddOrUpdate(
                    "CPU",
                    new BottleneckInfo
                    {
                        Resource = "CPU",
                        Description = $"High CPU usage: {_metrics.CpuUsagePercent:F1}%",
                        SeverityScore = (_metrics.CpuUsagePercent - 80) / 20 * 100,
                        DetectionTime = DateTime.UtcNow,
                        RecommendedAction = "Consider reducing concurrent validation limit"
                    },
                    (_, existing) =>
                    {
                        existing.SeverityScore = (_metrics.CpuUsagePercent - 80) / 20 * 100;
                        existing.DetectionTime = DateTime.UtcNow;
                        return existing;
                    });
            }
            else
            {
                _metrics.DetectedBottlenecks.TryRemove("CPU", out _);
            }

            // Memory bottleneck
            var memoryThresholdGb = 2.0;
            var memoryUsageGb = _metrics.MemoryUsageBytes / (1024.0 * 1024 * 1024);
            if (memoryUsageGb > memoryThresholdGb)
            {
                _metrics.DetectedBottlenecks.AddOrUpdate(
                    "Memory",
                    new BottleneckInfo
                    {
                        Resource = "Memory",
                        Description = $"High memory usage: {memoryUsageGb:F1}GB",
                        SeverityScore = (memoryUsageGb - memoryThresholdGb) / 2 * 100,
                        DetectionTime = DateTime.UtcNow,
                        RecommendedAction = "Trigger garbage collection and reduce batch sizes"
                    },
                    (_, existing) =>
                    {
                        existing.SeverityScore = (memoryUsageGb - memoryThresholdGb) / 2 * 100;
                        existing.DetectionTime = DateTime.UtcNow;
                        return existing;
                    });
            }
            else
            {
                _metrics.DetectedBottlenecks.TryRemove("Memory", out _);
            }

            // I/O bottleneck
            if (_metrics.DiskIoMbps > 100)
            {
                _metrics.DetectedBottlenecks.AddOrUpdate(
                    "DiskIO",
                    new BottleneckInfo
                    {
                        Resource = "Disk I/O",
                        Description = $"High disk I/O: {_metrics.DiskIoMbps:F1} MB/s",
                        SeverityScore = (_metrics.DiskIoMbps - 100) / 50 * 100,
                        DetectionTime = DateTime.UtcNow,
                        RecommendedAction = "Consider using memory caching for frequent operations"
                    },
                    (_, existing) =>
                    {
                        existing.SeverityScore = (_metrics.DiskIoMbps - 100) / 50 * 100;
                        existing.DetectionTime = DateTime.UtcNow;
                        return existing;
                    });
            }
            else
            {
                _metrics.DetectedBottlenecks.TryRemove("DiskIO", out _);
            }

            // Latency bottleneck
            if (_metrics.P95LatencyMs > 1000)
            {
                _metrics.DetectedBottlenecks.AddOrUpdate(
                    "Latency",
                    new BottleneckInfo
                    {
                        Resource = "Processing Latency",
                        Description = $"High P95 latency: {_metrics.P95LatencyMs:F1}ms",
                        SeverityScore = (_metrics.P95LatencyMs - 1000) / 1000 * 100,
                        DetectionTime = DateTime.UtcNow,
                        RecommendedAction = "Optimize validation logic or increase concurrency limit"
                    },
                    (_, existing) =>
                    {
                        existing.SeverityScore = (_metrics.P95LatencyMs - 1000) / 1000 * 100;
                        existing.DetectionTime = DateTime.UtcNow;
                        return existing;
                    });
            }
            else
            {
                _metrics.DetectedBottlenecks.TryRemove("Latency", out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _metricsUpdateTimer?.Dispose();
                _cpuCounter?.Dispose();
                _diskCounter?.Dispose();
                _disposed = true;
            }
        }

        private class ValidationInfo
        {
            public DateTime StartTime { get; set; }
            public int CodeSizeBytes { get; set; }
            public ValidationStatus Status { get; set; }
        }

        private enum ValidationStatus
        {
            Running,
            Completed,
            Failed
        }
    }
}