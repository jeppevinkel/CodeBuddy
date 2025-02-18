using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class DashboardOptions
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TimeGranularity { get; set; }
        public TimeSpan TrendAnalysisPeriod { get; set; }
        public ResourceMetricType[] ResourceTypes { get; set; }
    }

    public class DashboardData
    {
        public List<ResourceUsageData> HistoricalData { get; set; }
        public List<ResourceTrend> ResourceTrends { get; set; }
        public List<ResourceBottleneck> Bottlenecks { get; set; }
        public RealtimeMetrics RealtimeMetrics { get; set; }
        public TimeRange TimeRange { get; set; }
        public ResourceThresholds ResourceThresholds { get; set; }
        public List<AlertHistoryEntry> AlertHistory { get; set; }
        public List<ResourcePrediction> Predictions { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public List<OptimizationRecommendation> OptimizationRecommendations { get; set; }
    }

    public class TimeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Granularity { get; set; }
    }

    public class ResourcePrediction
    {
        public ResourceMetricType ResourceType { get; set; }
        public double PredictedValue { get; set; }
        public DateTime PredictionTime { get; set; }
        public double Confidence { get; set; }
        public DateTime? ThresholdCrossingTime { get; set; }
    }

    public class PerformanceMetrics
    {
        public double AverageResponseTime { get; set; }
        public double Throughput { get; set; }
        public double ErrorRate { get; set; }
        public int ConcurrentOperations { get; set; }
        public double ResourceEfficiency { get; set; }
    }

    public class ResourceUtilization
    {
        public UtilizationMetric CpuUtilization { get; set; }
        public UtilizationMetric MemoryUtilization { get; set; }
        public UtilizationMetric DiskUtilization { get; set; }
        public UtilizationMetric NetworkUtilization { get; set; }
    }

    public class UtilizationMetric
    {
        public double Current { get; set; }
        public double Peak { get; set; }
        public double Average { get; set; }
        public TrendDirection Trend { get; set; }
    }

    public class OptimizationRecommendation
    {
        public ResourceMetricType ResourceType { get; set; }
        public AlertSeverity Priority { get; set; }
        public string Impact { get; set; }
        public string RecommendedAction { get; set; }
        public string ExpectedImprovement { get; set; }
        public List<string> Implementation { get; set; }
        public TimeSpan TimeToImplement { get; set; }
    }

    public class RealtimeMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double PeakCpuUsage { get; set; }
        public double AverageCpuUsage { get; set; }
        public TrendDirection CpuUsageTrend { get; set; }

        public double MemoryUsagePercent { get; set; }
        public double PeakMemoryUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public TrendDirection MemoryUsageTrend { get; set; }

        public double DiskUtilizationPercent { get; set; }
        public double PeakDiskUtilization { get; set; }
        public double AverageDiskUtilization { get; set; }
        public TrendDirection DiskUtilizationTrend { get; set; }

        public double NetworkUtilizationPercent { get; set; }
        public double PeakNetworkUtilization { get; set; }
        public double AverageNetworkUtilization { get; set; }
        public TrendDirection NetworkUtilizationTrend { get; set; }
    }

    public enum TrendDirection
    {
        Decreasing,
        Stable,
        Increasing
    }
}