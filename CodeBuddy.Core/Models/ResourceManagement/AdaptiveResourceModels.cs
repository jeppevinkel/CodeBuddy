using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.ResourceManagement
{
    public class ResourceUsagePattern
    {
        public DateTime Timestamp { get; set; }
        public string ValidationType { get; set; }
        public long MemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
        public double CpuUsage { get; set; }
    }

    public class ResourcePrediction
    {
        public string ValidationType { get; set; }
        public long ExpectedMemoryUsage { get; set; }
        public int RecommendedThreadCount { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public double PredictionConfidence { get; set; }
    }

    public class AdaptiveResourceConfig
    {
        public int MinThreadPoolSize { get; set; } = 2;
        public int MaxThreadPoolSize { get; set; } = 32;
        public int MinQueueSize { get; set; } = 10;
        public int MaxQueueSize { get; set; } = 1000;
        public long MinMemoryThreshold { get; set; } = 100 * 1024 * 1024; // 100MB
        public long MaxMemoryThreshold { get; set; } = 4096 * 1024 * 1024; // 4GB
        public TimeSpan ResourceCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        public int BatchSize { get; set; } = 10;
        public double PredictionThreshold { get; set; } = 0.8; // 80% confidence threshold
    }

    public class ResourceOptimizationMetrics
    {
        public double PredictionAccuracy { get; set; }
        public int ResourceUtilizationPercentage { get; set; }
        public int EmergencyCleanupCount { get; set; }
        public double AverageResponseTime { get; set; }
        public int ResourceExhaustionCount { get; set; }
        public Dictionary<string, double> ValidationTypePerformance { get; set; }
        public List<string> Recommendations { get; set; }
    }
}