using System;

namespace CodeBuddy.Core.Models.Analytics
{
    public class UsagePattern
    {
        public string ResourceType { get; set; }
        public double CurrentUsage { get; set; }
        public double OptimalUsage { get; set; }
        public bool IndicatesInefficiency { get; set; }
        public string Analysis { get; set; }
        public double PotentialImpact { get; set; }
    }

    public class TrendAnalysis
    {
        public string Trend { get; set; }
        public double GrowthRate { get; set; }
    }

    public class LinearTrend
    {
        public double Slope { get; set; }
        public double Intercept { get; set; }

        public double Predict(int x) => Slope * x + Intercept;
    }

    public class ResourceOptimizationRecommendation
    {
        public string ResourceType { get; set; }
        public double CurrentUsage { get; set; }
        public double RecommendedUsage { get; set; }
        public double Impact { get; set; }
        public string Justification { get; set; }
    }

    public class ResourceBottleneck
    {
        public string ResourceType { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Severity { get; set; }
        public double Impact { get; set; }
    }

    public class PredictedResourceUsage
    {
        public double PredictedCpuUsage { get; set; }
        public double PredictedMemoryUsage { get; set; }
        public double PredictedDiskIO { get; set; }
        public double Confidence { get; set; }
    }

    public class ResourceUsageData
    {
        public double CpuUsagePercentage { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskIOBytesPerSecond { get; set; }
        public long Gen0SizeBytes { get; set; }
        public long Gen1SizeBytes { get; set; }
        public long Gen2SizeBytes { get; set; }
        public long LohSizeBytes { get; set; }
        public int FinalizationQueueLength { get; set; }
        public double FragmentationPercent { get; set; }
        public string PipelineId { get; set; }
        public string ValidatorType { get; set; }
    }

    public class ResourceUsageReport
    {
        public TimeSpan Period { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ResourceMetrics AverageMetrics { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public ThrottlingEvents ThrottlingEvents { get; set; }
    }

    public class ResourceMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskIORate { get; set; }
        public double NetworkUsage { get; set; }
        public double CacheHitRate { get; set; }
        public double ValidationLatency { get; set; }
    }

    public class ThrottlingEvents
    {
        public int TotalEvents { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public double AverageImpact { get; set; }
        public DateTime LastOccurrence { get; set; }
    }
}