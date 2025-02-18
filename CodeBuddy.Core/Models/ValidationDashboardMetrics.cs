using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class ValidationDashboardMetrics
    {
        public Dictionary<string, int> ValidationQueueMetrics { get; set; } = new();
        public Dictionary<string, double> ResourcePoolUtilization { get; set; } = new();
        public Dictionary<string, ResourcePredictionMetrics> AdaptiveResourceMetrics { get; set; } = new();
        public double MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ActiveHandles { get; set; }
        public string HealthStatus { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; } = new();

        public ValidationDashboardMetrics()
        {
            ValidationQueueMetrics = new Dictionary<string, int>();
            ResourcePoolUtilization = new Dictionary<string, double>();
            AdaptiveResourceMetrics = new Dictionary<string, ResourcePredictionMetrics>();
            Recommendations = new List<ResourceOptimizationRecommendation>();
            HealthStatus = "Healthy";
        }
    }

    public class ResourcePredictionMetrics
    {
        public double PredictionAccuracy { get; set; }
        public long PredictedMemoryUsage { get; set; }
        public int OptimalThreadCount { get; set; }
        public int RecommendedQueueSize { get; set; }
        public int RecommendedBatchSize { get; set; }
        public TimeSpan EstimatedProcessingTime { get; set; }
        public double ResourceUtilizationEfficiency { get; set; }
        public Dictionary<string, double> ResourceTrends { get; set; } = new();
        public Dictionary<string, double> PerformanceIndicators { get; set; } = new();
    }

    public class ResourceOptimizationRecommendation
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public string RecommendedAction { get; set; }
        public int Priority { get; set; }
        public double ExpectedImprovement { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> SupportingMetrics { get; set; } = new();
    }
}