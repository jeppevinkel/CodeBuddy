using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics;

public class ValidationTelemetryEvent
{
    public string ValidationId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public int RetryAttempts { get; set; }
    public List<string> FailedMiddleware { get; set; }
    public ResourceMetricsData ResourceMetrics { get; set; }
    public Dictionary<string, MiddlewarePerformanceMetrics> MiddlewarePerformance { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; }
}

public class ResourceMetricsData
{
    public double PeakCpuUsage { get; set; }
    public double PeakMemoryUsage { get; set; }
    public double PeakDiskIo { get; set; }
    public int PeakThreadCount { get; set; }
}

public class MiddlewarePerformanceMetrics
{
    public TimeSpan TotalDuration { get; set; }
    public int ExecutionCount { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; }
}

public class MetricsDataPoint
{
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; }
    public Dictionary<string, string> Tags { get; set; }
    public Dictionary<string, double> Values { get; set; }
}

public class ValidationMetricsUpdate
{
    public string ValidationId { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public ResourceMetrics ResourceUsage { get; set; }
}

public class ResourceMetricsUpdate
{
    public DateTime Timestamp { get; set; }
    public ResourceMetrics Metrics { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; }
}

public class ValidationAnalyticsData
{
    public string ValidationId { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int RetryAttempts { get; set; }
    public List<string> FailedMiddleware { get; set; }
    public ResourceMetrics ResourceMetrics { get; set; }
    public Dictionary<string, MiddlewarePerformanceData> MiddlewarePerformance { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; }
}

public class ValidationTelemetryAnalysis
{
    public Dictionary<string, double> AggregateMetrics { get; set; }
    public List<TelemetryPattern> Patterns { get; set; }
    public List<TelemetryAnomaly> Anomalies { get; set; }
    public Dictionary<string, List<double>> Correlations { get; set; }
    public Dictionary<string, TrendAnalysis> Trends { get; set; }
}

public class TelemetryPattern
{
    public string PatternName { get; set; }
    public string MetricName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, double> Properties { get; set; }
}

public class TelemetryAnomaly
{
    public string MetricName { get; set; }
    public DateTime Timestamp { get; set; }
    public double ExpectedValue { get; set; }
    public double ActualValue { get; set; }
    public double Severity { get; set; }
    public string Description { get; set; }
}

public class TimeSeriesPattern
{
    public string PatternType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double Strength { get; set; }
    public Dictionary<string, double> Properties { get; set; }
}

public class TimeSeriesAnomaly
{
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; }
    public double Value { get; set; }
    public double Score { get; set; }
    public string Description { get; set; }
}

public class ResourceUsageTrend
{
    public string ResourceType { get; set; }
    public TrendDirection Direction { get; set; }
    public double Rate { get; set; }
    public double Confidence { get; set; }
    public string Analysis { get; set; }
}

public class PerformanceAnomaly
{
    public DateTime DetectionTime { get; set; }
    public string AnomalyType { get; set; }
    public double Severity { get; set; }
    public string Description { get; set; }
    public Dictionary<string, double> RelatedMetrics { get; set; }
}

public class ResourceOptimizationReport
{
    public List<OptimizationRecommendation> Recommendations { get; set; }
    public Dictionary<string, double> PotentialImpact { get; set; }
    public Dictionary<string, ResourceEfficiency> CurrentEfficiency { get; set; }
}

public class ResourceBottleneck
{
    public string ResourceType { get; set; }
    public double Severity { get; set; }
    public string Impact { get; set; }
    public string RecommendedAction { get; set; }
}

public class CapacityPlanningReport
{
    public Dictionary<string, ResourceForecast> ResourceForecasts { get; set; }
    public List<ScalingRecommendation> ScalingRecommendations { get; set; }
    public Dictionary<string, double> UtilizationPredictions { get; set; }
}

public class ValidationPatternInsight
{
    public string PatternName { get; set; }
    public double Frequency { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public Dictionary<string, double> Characteristics { get; set; }
    public string Impact { get; set; }
}

public class SLAComplianceReport
{
    public Dictionary<string, double> ComplianceMetrics { get; set; }
    public List<SLAViolation> Violations { get; set; }
    public Dictionary<string, TrendAnalysis> Trends { get; set; }
}

public class PerformanceForecast
{
    public DateTime ForecastTime { get; set; }
    public Dictionary<string, double> PredictedMetrics { get; set; }
    public double Confidence { get; set; }
    public List<string> Factors { get; set; }
}

public class AnalyticsPipelineConfig
{
    public Dictionary<string, AnalysisPipelineSettings> PipelineSettings { get; set; }
    public Dictionary<string, AlertThreshold> AlertThresholds { get; set; }
    public RetentionPolicy RetentionPolicy { get; set; }
}

public class AnalyticsDashboardState
{
    public DateTime LastUpdateTime { get; set; }
    public Dictionary<string, MetricState> MetricStates { get; set; }
    public List<ActiveAlert> ActiveAlerts { get; set; }
    public Dictionary<string, PipelineState> PipelineStates { get; set; }
}

public class DateTimeRange
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public enum TrendDirection
{
    Increasing,
    Decreasing,
    Stable,
    Fluctuating
}

public class TrendAnalysis
{
    public TrendDirection Direction { get; set; }
    public double Rate { get; set; }
    public double Seasonality { get; set; }
    public Dictionary<string, double> Factors { get; set; }
}

public class DashboardMetrics
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> CurrentValues { get; set; }
    public Dictionary<string, TrendAnalysis> Trends { get; set; }
    public List<Alert> ActiveAlerts { get; set; }
}

public class ValidationPerformanceReport
{
    public TimeSpan AverageExecutionTime { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, double> ResourceUtilization { get; set; }
    public List<PerformanceAnomaly> Anomalies { get; set; }
}

public class ResourceUtilizationReport
{
    public Dictionary<string, ResourceUsageTrend> Trends { get; set; }
    public List<ResourceBottleneck> Bottlenecks { get; set; }
    public Dictionary<string, double> Efficiency { get; set; }
}

public class DashboardAlertConfig
{
    public Dictionary<string, AlertThreshold> Thresholds { get; set; }
    public NotificationSettings NotificationSettings { get; set; }
    public List<string> EnabledAlerts { get; set; }
}

public class DashboardState
{
    public DateTime LastRefresh { get; set; }
    public Dictionary<string, MetricState> Metrics { get; set; }
    public List<Alert> ActiveAlerts { get; set; }
}

public class Alert
{
    public string AlertId { get; set; }
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; }
}

public class AlertThreshold
{
    public double WarningLevel { get; set; }
    public double CriticalLevel { get; set; }
    public TimeSpan Duration { get; set; }
}

public class MetricState
{
    public double CurrentValue { get; set; }
    public TrendDirection Trend { get; set; }
    public DateTime LastUpdate { get; set; }
}