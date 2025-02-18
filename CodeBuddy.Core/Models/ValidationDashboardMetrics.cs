using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class ValidationDashboardMetrics
    {
        public Dictionary<string, int> ValidationQueueMetrics { get; set; } = new();
        public Dictionary<string, double> ResourcePoolUtilization { get; set; } = new();
        public Dictionary<string, ResourcePredictionMetrics> AdaptiveResourceMetrics { get; set; } = new();
        public Dictionary<string, SecurityMetrics> SecurityMetricsByLanguage { get; set; } = new();
        public Dictionary<string, int> SecurityViolationsByCategory { get; set; } = new();
        public Dictionary<string, int> SecurityViolationsBySeverity { get; set; } = new();
        public double MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ActiveHandles { get; set; }
        public string HealthStatus { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; } = new();
        public List<SecurityOptimizationRecommendation> SecurityRecommendations { get; set; } = new();

        public ValidationDashboardMetrics()
        {
            ValidationQueueMetrics = new Dictionary<string, int>();
            ResourcePoolUtilization = new Dictionary<string, double>();
            AdaptiveResourceMetrics = new Dictionary<string, ResourcePredictionMetrics>();
            SecurityMetricsByLanguage = new Dictionary<string, SecurityMetrics>();
            SecurityViolationsByCategory = new Dictionary<string, int>();
            SecurityViolationsBySeverity = new Dictionary<string, int>();
            Recommendations = new List<ResourceOptimizationRecommendation>();
            SecurityRecommendations = new List<SecurityOptimizationRecommendation>();
            HealthStatus = "Healthy";
        }
    }

    public class SecurityMetrics
    {
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public int HighViolations { get; set; }
        public int MediumViolations { get; set; }
        public int LowViolations { get; set; }
        public int VulnerableDependencies { get; set; }
        public Dictionary<string, double> ViolationTrends { get; set; } = new();
        public Dictionary<string, int> ViolationsByRule { get; set; } = new();
        public TimeSpan AverageScanTime { get; set; }
        public int TotalScans { get; set; }
        public DateTime LastScanTime { get; set; }
    }

    public class SecurityOptimizationRecommendation
    {
        public string Category { get; set; }
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string RecommendedAction { get; set; }
        public int ViolationCount { get; set; }
        public string Severity { get; set; }
        public DateTime DetectionTime { get; set; }
        public bool IsResolved { get; set; }
        public List<string> AffectedFiles { get; set; } = new();
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