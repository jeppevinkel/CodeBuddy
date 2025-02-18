using System;
using System.Collections.Concurrent;

namespace CodeBuddy.Core.Models
{
    public class PipelineMetrics
    {
        // Throughput metrics
        public long TotalRequestsProcessed { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }

        // Resource utilization
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public double DiskIoMbps { get; set; }
        public int ActiveThreads { get; set; }
        public int QueuedRequests { get; set; }

        // Bottleneck indicators
        public ConcurrentDictionary<string, BottleneckInfo> DetectedBottlenecks { get; } = new();
        public double QueueSaturationPercent { get; set; }
        public double ResourceSaturationPercent { get; set; }

        // Health indicators
        public bool CircuitBreakerOpen { get; set; }
        public int FailedRequests { get; set; }
        public int StalledValidations { get; set; }
        public TimeSpan UptimeTotal { get; set; }
        public DateTime LastResetTime { get; set; }
    }

    public class BottleneckInfo
    {
        public string Resource { get; set; }
        public string Description { get; set; }
        public double SeverityScore { get; set; }
        public DateTime DetectionTime { get; set; }
        public string RecommendedAction { get; set; }
    }
}