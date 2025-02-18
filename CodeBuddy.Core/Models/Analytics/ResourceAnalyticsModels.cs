using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class ResourceUsageData
    {
        public string PipelineId { get; set; }
        public string ValidatorType { get; set; }
        public double CpuUsagePercentage { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskIOBytesPerSecond { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ResourceUsageReport
    {
        public TimeSpan Period { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, double> AverageMetrics { get; set; }
        public List<ThrottlingEvent> ThrottlingEvents { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
    }

    public class ResourceOptimizationRecommendation
    {
        public string ResourceType { get; set; }
        public double CurrentUsage { get; set; }
        public double RecommendedUsage { get; set; }
        public string Impact { get; set; }
        public string Justification { get; set; }
    }

    public class ResourceUsageTrends
    {
        public TrendAnalysis CpuTrend { get; set; }
        public TrendAnalysis MemoryTrend { get; set; }
        public TrendAnalysis DiskIOTrend { get; set; }
        public PredictedResourceUsage PredictedUsage { get; set; }
        public Dictionary<string, TrendAnalysis> ValidatorTrends { get; set; }
        public TimeSpan AnalysisPeriod { get; set; }
        public List<ResourceEfficiencyMetric> EfficiencyMetrics { get; set; }
    }

    public class ResourceEfficiencyMetric
    {
        public string MetricName { get; set; }
        public double CurrentValue { get; set; }
        public double OptimalValue { get; set; }
        public string OptimizationSuggestion { get; set; }
        public double PotentialImprovementPercent { get; set; }
    }

    public class DashboardView
    {
        public string ViewName { get; set; }
        public string Description { get; set; }
        public List<DashboardWidget> Widgets { get; set; }
        public Dictionary<string, string> Configuration { get; set; }
    }

    public class DashboardWidget
    {
        public string WidgetId { get; set; }
        public string WidgetType { get; set; }
        public string Title { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public Dictionary<string, string> Configuration { get; set; }
    }

    public class ResourceMetricExport
    {
        public DateTime ExportTimestamp { get; set; }
        public TimeRange TimeRange { get; set; }
        public List<ResourceUsageData> UsageData { get; set; }
        public List<ResourceBottleneck> Bottlenecks { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; }
        public ResourceUsageTrends Trends { get; set; }
    }

    public class TimeRange
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Resolution { get; set; }
    }

    public class ResourceBottleneck
    {
        public string ResourceType { get; set; }
        public DateTime OccurrenceTime { get; set; }
        public double Severity { get; set; }
        public string Impact { get; set; }
        public List<string> AffectedOperations { get; set; }
        public string RecommendedAction { get; set; }
    }

    public class TimeSeriesDataPoint
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class TrendAnalysis
    {
        public double Slope { get; set; }
        public double Correlation { get; set; }
        public double Seasonality { get; set; }
        public List<double> TrendLine { get; set; }
    }

    public class PredictedResourceUsage
    {
        public double PredictedCpuUsage { get; set; }
        public double PredictedMemoryUsage { get; set; }
        public double PredictedDiskIORate { get; set; }
        public DateTime PredictionTimestamp { get; set; }
        public double Confidence { get; set; }
    }

    public class ResourceUtilization
    {
        public double AverageCpuUtilization { get; set; }
        public double PeakCpuUtilization { get; set; }
        public double AverageMemoryUtilization { get; set; }
        public double PeakMemoryUtilization { get; set; }
        public double AverageDiskIOUtilization { get; set; }
        public double PeakDiskIOUtilization { get; set; }
    }

    public class ThrottlingEvent
    {
        public DateTime Timestamp { get; set; }
        public string ResourceType { get; set; }
        public double ThresholdValue { get; set; }
        public double ActualValue { get; set; }
        public TimeSpan Duration { get; set; }
        public string Impact { get; set; }
    }
}