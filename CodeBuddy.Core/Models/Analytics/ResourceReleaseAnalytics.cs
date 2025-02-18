using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ResourceReleaseAnalytics
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public ResourceMetrics Metrics { get; set; }
        public List<ResourceLeakPattern> LeakPatterns { get; set; }
        public ResourceHealthStatus HealthStatus { get; set; }
    }

    public class ResourceMetrics
    {
        public int ActiveAllocations { get; set; }
        public int TotalAllocations { get; set; }
        public int ReleasedResources { get; set; }
        public int OrphanedResources { get; set; }
        public double AverageTimeToRelease { get; set; }
        public Dictionary<string, int> ResourceTypeDistribution { get; set; }
    }

    public class ResourceLeakPattern
    {
        public string PatternId { get; set; }
        public string Description { get; set; }
        public double Frequency { get; set; }
        public string ResourceType { get; set; }
        public string OwnerPattern { get; set; }
        public List<string> AffectedResources { get; set; }
    }

    public enum ResourceHealthStatus
    {
        Healthy,
        Warning,
        Critical
    }

    public class ResourceReleaseThresholds
    {
        public TimeSpan MaxAllocationTime { get; set; }
        public int MaxActiveAllocations { get; set; }
        public double MaxOrphanedRatio { get; set; }
        public Dictionary<string, int> ResourceTypeLimits { get; set; }
    }
}