using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class DashboardData
    {
        public ResourceMetricsModel CurrentMetrics { get; set; }
        public ResourceUsageTrends Trends { get; set; }
        public List<ResourceBottleneck> Bottlenecks { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; }
        public List<ResourceAlert> Alerts { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
    }

    public class ResourceWidget
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
        public List<string> MetricTypes { get; set; }
        public string RefreshInterval { get; set; }
    }

    public class AlertThreshold
    {
        public double Warning { get; set; }
        public double Critical { get; set; }
    }

    public class PerformanceMetrics
    {
        public Dictionary<string, double> ValidatorPerformance { get; set; }
        public CacheMetrics CacheMetrics { get; set; }
        public ResourceEfficiency ResourceEfficiency { get; set; }
    }

    public class CacheMetrics
    {
        public double HitRate { get; set; }
        public double MissRate { get; set; }
        public long CacheSize { get; set; }
        public long ItemCount { get; set; }
        public double EvictionRate { get; set; }
        public TimeSpan AverageAccessTime { get; set; }
    }

    public class ResourceEfficiency
    {
        public double ResourceUtilizationScore { get; set; }
        public double ResourceWasteScore { get; set; }
        public Dictionary<string, double> ComponentEfficiency { get; set; }
        public List<string> OptimizationSuggestions { get; set; }
    }

    public class ResourceUtilization
    {
        public double CpuUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public double DiskUtilization { get; set; }
        public double NetworkUtilization { get; set; }
    }

    public class TimeSeriesDataPoint
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public double DiskIORate { get; set; }
        public double NetworkUsage { get; set; }
    }
}