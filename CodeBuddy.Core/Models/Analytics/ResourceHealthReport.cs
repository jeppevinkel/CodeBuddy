using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ResourceHealthReport
    {
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, ResourceHealthMetrics> ResourceMetrics { get; set; }
        public List<ResourceAlert> Alerts { get; set; }
        public Dictionary<string, ResourceUsageStatistics> UsageStatistics { get; set; }
        public List<ResourceTrend> Trends { get; set; }
    }

    public class ResourceHealthMetrics
    {
        public string ResourceName { get; set; }
        public int CurrentAllocationCount { get; set; }
        public int PeakAllocationCount { get; set; }
        public double AverageUsage { get; set; }
        public TimeSpan AverageLifetime { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public double HealthScore { get; set; }
    }

    public class ResourceAlert
    {
        public string ResourceName { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public DateTime DetectedAt { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class ResourceUsageStatistics
    {
        public string ResourceName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalAllocations { get; set; }
        public int TotalDeallocations { get; set; }
        public double AverageUsagePercent { get; set; }
        public TimeSpan AverageAllocationDuration { get; set; }
        public int OrphanedResourceCount { get; set; }
        public Dictionary<string, double> ComponentUsageDistribution { get; set; }
    }

    public class ResourceTrend
    {
        public string ResourceName { get; set; }
        public string TrendType { get; set; }
        public double TrendValue { get; set; }
        public string Analysis { get; set; }
        public Dictionary<string, object> TrendMetrics { get; set; }
    }
}