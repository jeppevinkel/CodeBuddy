using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ResourceMetricsModel
    {
        public DateTime Timestamp { get; set; }
        public long MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ActiveHandles { get; set; }
        public int TemporaryFiles { get; set; }
        public Dictionary<string, double> ValidationQueueMetrics { get; set; } = new();
        public Dictionary<string, TimeSpan> ValidationPhaseTimings { get; set; } = new();
        public Dictionary<string, double> ResourcePoolUtilization { get; set; } = new();
        public List<string> ActiveAlerts { get; set; } = new();
        public string HealthStatus { get; set; }
    }

    public class ResourceTrendData
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<ResourceMetricsModel> Metrics { get; set; } = new();
        public Dictionary<string, double> AverageUtilization { get; set; } = new();
        public Dictionary<string, double> PeakUtilization { get; set; } = new();
        public List<ResourceAlert> Alerts { get; set; } = new();
    }

    public class ResourceAlert
    {
        public string AlertId { get; set; }
        public string ResourceType { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Thresholds { get; set; }
        public Dictionary<string, double> CurrentValues { get; set; }
    }

    public class ResourceThresholds
    {
        public long MemoryWarningThresholdBytes { get; set; } = 500 * 1024 * 1024; // 500MB
        public long MemoryCriticalThresholdBytes { get; set; } = 800 * 1024 * 1024; // 800MB
        public double CpuWarningThresholdPercent { get; set; } = 70;
        public double CpuCriticalThresholdPercent { get; set; } = 90;
        public int MaxHandleCount { get; set; } = 1000;
        public int MaxTemporaryFiles { get; set; } = 100;
        public double QueueSaturationThreshold { get; set; } = 0.8; // 80%
        public TimeSpan MaxValidationPhaseTime { get; set; } = TimeSpan.FromSeconds(30);
    }
}