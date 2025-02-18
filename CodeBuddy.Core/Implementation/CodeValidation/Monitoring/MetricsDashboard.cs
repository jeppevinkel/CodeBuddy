using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsDashboard
    {
        Task StartMonitoring(CancellationToken cancellationToken = default);
        Task StopMonitoring();
        Task<DashboardData> GetDashboardData(TimeRange timeRange = null);
        Task<DashboardView> GetCustomDashboardView(string viewName);
        Task SaveCustomDashboardView(DashboardView view);
        Task<ResourceMetricExport> ExportMetrics(TimeRange timeRange);
        void SetAlertThreshold(string metricName, double threshold, AlertSeverity severity);
        Task SetCustomAlertRule(AlertRule rule);
        IEnumerable<Alert> GetActiveAlerts();
        Task<ResourceUsageTrends> GetResourceTrends(TimeRange timeRange);
        Task<List<ResourceOptimizationRecommendation>> GetOptimizationRecommendations();
        Task<List<ResourceBottleneck>> GetResourceBottlenecks(TimeRange timeRange);
        Task<PerformanceComparison> ComparePerformance(TimeRange period1, TimeRange period2);
        Task<ResourceUsagePatterns> AnalyzeResourcePatterns(TimeRange timeRange);
        Task<BottleneckCorrelation> AnalyzeBottleneckCorrelations(TimeRange timeRange);
        Task<CapacityPlanningInsights> GetCapacityPlanningInsights(TimeRange timeRange);
        Task<HeatMapData> GetValidationPipelineHeatMap(TimeRange timeRange);
    }

    public class MetricsDashboard : IMetricsDashboard
    {
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly Dictionary<string, (double Threshold, AlertSeverity Severity)> _alertThresholds = new();
        private readonly List<AlertRule> _customAlertRules = new();
        private readonly List<Alert> _activeAlerts = new();
        private readonly Dictionary<string, DashboardView> _customViews = new();
        private readonly ResourceAnalytics _resourceAnalytics;
        private readonly TimeSeriesStorage _timeSeriesStorage;
        private Timer _monitoringTimer;
        private const int MonitoringIntervalMs = 5000;

        public MetricsDashboard(
            IMetricsAggregator metricsAggregator,
            ResourceAnalytics resourceAnalytics,
            TimeSeriesStorage timeSeriesStorage)
        {
            _metricsAggregator = metricsAggregator;
            _resourceAnalytics = resourceAnalytics;
            _timeSeriesStorage = timeSeriesStorage;
        }

        public MetricsDashboard(IMetricsAggregator metricsAggregator)
        {
            _metricsAggregator = metricsAggregator;
        }

        public Task StartMonitoring(CancellationToken cancellationToken = default)
        {
            _monitoringTimer = new Timer(MonitoringCallback, null, 0, MonitoringIntervalMs);
            return Task.CompletedTask;
        }

        public Task StopMonitoring()
        {
            _monitoringTimer?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<DashboardData> GetDashboardData(TimeRange timeRange = null)
        {
            timeRange ??= new TimeRange 
            { 
                StartTime = DateTime.UtcNow.AddHours(-24), 
                EndTime = DateTime.UtcNow,
                Resolution = "5m"
            };

            var currentMetrics = _metricsAggregator.GetCurrentMetrics();
            var historicalMetrics = _metricsAggregator.GetHistoricalMetrics(timeRange.EndTime - timeRange.StartTime);
            var resourceTrends = await GetResourceTrends(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);
            var recommendations = await GetOptimizationRecommendations();

            var dashboardData = new DashboardData
            {
                CurrentMetrics = currentMetrics,
                PerformanceTrends = CalculatePerformanceTrends(historicalMetrics),
                Bottlenecks = bottlenecks,
                ResourceUtilization = currentMetrics.ResourceMetrics,
                ActiveAlerts = _activeAlerts.ToList(),
                ResourceTrends = resourceTrends,
                Recommendations = recommendations,
                TimeRange = timeRange
            };

            return dashboardData;
        }

        public async Task<DashboardView> GetCustomDashboardView(string viewName)
        {
            if (_customViews.TryGetValue(viewName, out var view))
            {
                await UpdateDashboardViewData(view);
                return view;
            }
            return null;
        }

        public Task SaveCustomDashboardView(DashboardView view)
        {
            _customViews[view.ViewName] = view;
            return Task.CompletedTask;
        }

        public async Task<ResourceMetricExport> ExportMetrics(TimeRange timeRange)
        {
            var resourceData = await _resourceAnalytics.GetResourceUsageData(timeRange);
            var trends = await GetResourceTrends(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);
            var recommendations = await GetOptimizationRecommendations();

            return new ResourceMetricExport
            {
                ExportTimestamp = DateTime.UtcNow,
                TimeRange = timeRange,
                UsageData = resourceData,
                Bottlenecks = bottlenecks,
                Recommendations = recommendations,
                Trends = trends
            };
        }

        public async Task<ResourceUsageTrends> GetResourceTrends(TimeRange timeRange)
        {
            var trends = await _resourceAnalytics.AnalyzeResourceTrends(timeRange);
            var validatorTrends = await _resourceAnalytics.AnalyzeValidatorTrends(timeRange);
            var efficiencyMetrics = await _resourceAnalytics.CalculateEfficiencyMetrics(timeRange);

            trends.ValidatorTrends = validatorTrends;
            trends.EfficiencyMetrics = efficiencyMetrics;
            trends.AnalysisPeriod = timeRange.EndTime - timeRange.StartTime;

            return trends;
        }

        public async Task<List<ResourceOptimizationRecommendation>> GetOptimizationRecommendations()
        {
            return await _resourceAnalytics.GenerateOptimizationRecommendations();
        }

        public async Task<List<ResourceBottleneck>> GetResourceBottlenecks(TimeRange timeRange)
        {
            return await _resourceAnalytics.IdentifyBottlenecks(timeRange);
        }

        private async Task UpdateDashboardViewData(DashboardView view)
        {
            foreach (var widget in view.Widgets)
            {
                widget.Data = await GetWidgetData(widget);
            }
        }

        private async Task<Dictionary<string, object>> GetWidgetData(DashboardWidget widget)
        {
            var timeRange = ParseTimeRange(widget.Configuration);
            
            return widget.WidgetType switch
            {
                "ResourceUsage" => new Dictionary<string, object>
                {
                    ["metrics"] = await _resourceAnalytics.GetResourceUsageData(timeRange)
                },
                "Trends" => new Dictionary<string, object>
                {
                    ["trends"] = await GetResourceTrends(timeRange)
                },
                "Bottlenecks" => new Dictionary<string, object>
                {
                    ["bottlenecks"] = await GetResourceBottlenecks(timeRange)
                },
                "Recommendations" => new Dictionary<string, object>
                {
                    ["recommendations"] = await GetOptimizationRecommendations()
                },
                "Alerts" => new Dictionary<string, object>
                {
                    ["alerts"] = GetActiveAlerts()
                },
                _ => new Dictionary<string, object>()
            };
        }

        private static TimeRange ParseTimeRange(Dictionary<string, string> config)
        {
            if (!config.TryGetValue("timeRange", out var range))
                range = "24h";

            var end = DateTime.UtcNow;
            var start = range switch
            {
                "1h" => end.AddHours(-1),
                "6h" => end.AddHours(-6),
                "24h" => end.AddHours(-24),
                "7d" => end.AddDays(-7),
                "30d" => end.AddDays(-30),
                _ => end.AddHours(-24)
            };

            return new TimeRange
            {
                StartTime = start,
                EndTime = end,
                Resolution = range
            };
        }

        public void SetAlertThreshold(string metricName, double threshold, AlertSeverity severity)
        {
            _alertThresholds[metricName] = (threshold, severity);
        }

        public Task SetCustomAlertRule(AlertRule rule)
        {
            _customAlertRules.Add(rule);
            return Task.CompletedTask;
        }

        public async Task<PerformanceComparison> ComparePerformance(TimeRange period1, TimeRange period2)
        {
            var metrics1 = await _timeSeriesStorage.GetMetrics(period1);
            var metrics2 = await _timeSeriesStorage.GetMetrics(period2);

            return new PerformanceComparison
            {
                Period1 = period1,
                Period2 = period2,
                CpuUsageComparison = CompareMetrics(
                    metrics1.Select(m => m.ResourceMetrics.CpuUsagePercent),
                    metrics2.Select(m => m.ResourceMetrics.CpuUsagePercent)),
                MemoryUsageComparison = CompareMetrics(
                    metrics1.Select(m => m.ResourceMetrics.MemoryUsageMB),
                    metrics2.Select(m => m.ResourceMetrics.MemoryUsageMB)),
                ThroughputComparison = CompareThroughput(metrics1, metrics2),
                LatencyComparison = CompareLatency(metrics1, metrics2),
                ResourceEfficiencyDelta = CalculateEfficiencyDelta(metrics1, metrics2)
            };
        }

        public async Task<ResourceUsagePatterns> AnalyzeResourcePatterns(TimeRange timeRange)
        {
            var metrics = await _timeSeriesStorage.GetMetrics(timeRange);
            return new ResourceUsagePatterns
            {
                TimeRange = timeRange,
                DailyPatterns = IdentifyDailyPatterns(metrics),
                WeeklyPatterns = IdentifyWeeklyPatterns(metrics),
                PeakUsagePeriods = IdentifyPeakPeriods(metrics),
                ResourceCorrelations = AnalyzeResourceCorrelations(metrics),
                AnomalyPatterns = DetectAnomalyPatterns(metrics)
            };
        }

        public async Task<BottleneckCorrelation> AnalyzeBottleneckCorrelations(TimeRange timeRange)
        {
            var metrics = await _timeSeriesStorage.GetMetrics(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);

            return new BottleneckCorrelation
            {
                TimeRange = timeRange,
                ResourceCorrelations = AnalyzeResourceBottleneckCorrelations(metrics, bottlenecks),
                WorkloadCorrelations = AnalyzeWorkloadCorrelations(metrics, bottlenecks),
                CascadingEffects = IdentifyCascadingBottlenecks(bottlenecks),
                ImpactAnalysis = AnalyzeBottleneckImpact(metrics, bottlenecks)
            };
        }

        public async Task<CapacityPlanningInsights> GetCapacityPlanningInsights(TimeRange timeRange)
        {
            var metrics = await _timeSeriesStorage.GetMetrics(timeRange);
            var patterns = await AnalyzeResourcePatterns(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);

            return new CapacityPlanningInsights
            {
                TimeRange = timeRange,
                GrowthTrends = AnalyzeGrowthTrends(metrics),
                ResourceProjections = ProjectResourceNeeds(metrics, patterns),
                BottleneckRisks = AssessBottleneckRisks(bottlenecks, patterns),
                ScalingRecommendations = GenerateScalingRecommendations(metrics, patterns),
                OptimizationOpportunities = IdentifyOptimizationOpportunities(metrics, bottlenecks)
            };
        }

        public async Task<HeatMapData> GetValidationPipelineHeatMap(TimeRange timeRange)
        {
            var metrics = await _timeSeriesStorage.GetMetrics(timeRange);
            return new HeatMapData
            {
                TimeRange = timeRange,
                ResourceIntensityMap = GenerateResourceIntensityMap(metrics),
                BottleneckHotspots = IdentifyBottleneckHotspots(metrics),
                ComponentLoadDistribution = AnalyzeComponentLoadDistribution(metrics),
                TimeBasedHeatMap = GenerateTimeBasedHeatMap(metrics)
            };
        }

        public IEnumerable<Alert> GetActiveAlerts()
        {
            return _activeAlerts.ToList();
        }

        private void MonitoringCallback(object state)
        {
            var metrics = _metricsAggregator.GetCurrentMetrics();
            CheckAlertThresholds(metrics);
        }

        private void CheckAlertThresholds(MetricsSummary metrics)
        {
            _activeAlerts.Clear();

            // Check standard thresholds
            foreach (var middleware in metrics.MiddlewareMetrics)
            {
                var failureRate = CalculateFailureRate(middleware.Value);
                if (_alertThresholds.TryGetValue($"{middleware.Key}_FailureRate", out var threshold)
                    && failureRate > threshold.Threshold)
                {
                    _activeAlerts.Add(new Alert
                    {
                        Severity = threshold.Severity,
                        Message = $"High failure rate ({failureRate:P2}) detected in {middleware.Key}",
                        Timestamp = DateTime.UtcNow,
                        MetricName = $"{middleware.Key}_FailureRate",
                        CurrentValue = failureRate,
                        ThresholdValue = threshold.Threshold
                    });
                }
            }

            // Check resource metrics
            CheckResourceMetrics(metrics);

            // Evaluate custom alert rules
            EvaluateCustomAlertRules(metrics);
        }

        private void CheckResourceMetrics(MetricsSummary metrics)
        {
            if (_alertThresholds.TryGetValue("CpuUsage", out var cpuThreshold)
                && metrics.ResourceMetrics.CpuUsagePercent > cpuThreshold.Threshold)
            {
                _activeAlerts.Add(new Alert
                {
                    Severity = cpuThreshold.Severity,
                    Message = $"High CPU usage detected: {metrics.ResourceMetrics.CpuUsagePercent:F1}%",
                    Timestamp = DateTime.UtcNow,
                    MetricName = "CpuUsage",
                    CurrentValue = metrics.ResourceMetrics.CpuUsagePercent,
                    ThresholdValue = cpuThreshold.Threshold
                });
            }

            if (_alertThresholds.TryGetValue("MemoryUsage", out var memoryThreshold)
                && metrics.ResourceMetrics.MemoryUsageMB > memoryThreshold.Threshold)
            {
                _activeAlerts.Add(new Alert
                {
                    Severity = memoryThreshold.Severity,
                    Message = $"High memory usage detected: {metrics.ResourceMetrics.MemoryUsageMB:F1} MB",
                    Timestamp = DateTime.UtcNow,
                    MetricName = "MemoryUsage",
                    CurrentValue = metrics.ResourceMetrics.MemoryUsageMB,
                    ThresholdValue = memoryThreshold.Threshold
                });
            }
        }

        private void EvaluateCustomAlertRules(MetricsSummary metrics)
        {
            foreach (var rule in _customAlertRules)
            {
                if (rule.EvaluateCondition(metrics))
                {
                    _activeAlerts.Add(new Alert
                    {
                        Severity = rule.Severity,
                        Message = rule.GenerateAlertMessage(metrics),
                        Timestamp = DateTime.UtcNow,
                        MetricName = rule.MetricName,
                        CurrentValue = rule.GetCurrentValue(metrics),
                        ThresholdValue = rule.ThresholdValue
                    });
                }
            }
        }

        private static double CalculateFailureRate(MiddlewareMetrics metrics)
        {
            var total = metrics.SuccessCount + metrics.FailureCount;
            return total == 0 ? 0 : (double)metrics.FailureCount / total;
        }

        private static PerformanceTrends CalculatePerformanceTrends(IEnumerable<MetricsSummary> historicalMetrics)
        {
            var metrics = historicalMetrics.ToList();
            return new PerformanceTrends
            {
                AverageResponseTime = metrics.SelectMany(m => 
                    m.MiddlewareMetrics.Values.Select(v => v.AverageExecutionTime)).Average(),
                SuccessRate = metrics.SelectMany(m =>
                    m.MiddlewareMetrics.Values.Select(CalculateFailureRate)).Average(),
                ResourceUtilization = new ResourceUtilizationTrend
                {
                    AverageCpuUsage = metrics.Select(m => m.ResourceMetrics.CpuUsagePercent).Average(),
                    AverageMemoryUsage = metrics.Select(m => m.ResourceMetrics.MemoryUsageMB).Average(),
                    AverageActiveThreads = metrics.Select(m => m.ResourceMetrics.ActiveThreads).Average()
                }
            };
        }

        private static List<BottleneckInfo> IdentifyBottlenecks(MetricsSummary metrics)
        {
            var bottlenecks = new List<BottleneckInfo>();

            foreach (var middleware in metrics.MiddlewareMetrics)
            {
                if (middleware.Value.AverageExecutionTime > 1000) // 1 second threshold
                {
                    bottlenecks.Add(new BottleneckInfo
                    {
                        Component = middleware.Key,
                        AverageLatency = middleware.Value.AverageExecutionTime,
                        ImpactLevel = CalculateImpactLevel(middleware.Value)
                    });
                }
            }

            return bottlenecks;
        }

        private static ImpactLevel CalculateImpactLevel(MiddlewareMetrics metrics)
        {
            var failureRate = CalculateFailureRate(metrics);
            return failureRate switch
            {
                > 0.25 => ImpactLevel.High,
                > 0.10 => ImpactLevel.Medium,
                _ => ImpactLevel.Low
            };
        }
    }

    public class DashboardData
    {
        public MetricsSummary CurrentMetrics { get; set; }
        public PerformanceTrends PerformanceTrends { get; set; }
        public List<ResourceBottleneck> Bottlenecks { get; set; }
        public ResourceMetrics ResourceUtilization { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
        public ResourceUsageTrends ResourceTrends { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; }
        public TimeRange TimeRange { get; set; }
    }

    public class PerformanceTrends
    {
        public double AverageResponseTime { get; set; }
        public double SuccessRate { get; set; }
        public ResourceUtilizationTrend ResourceUtilization { get; set; }
    }

    public class ResourceUtilizationTrend
    {
        public double AverageCpuUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public double AverageActiveThreads { get; set; }
    }

    public class BottleneckInfo
    {
        public string Component { get; set; }
        public double AverageLatency { get; set; }
        public ImpactLevel ImpactLevel { get; set; }
    }

    public class Alert
    {
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ImpactLevel
    {
        Low,
        Medium,
        High
    }

    public class AlertRule
    {
        public string MetricName { get; set; }
        public string Description { get; set; }
        public AlertSeverity Severity { get; set; }
        public double ThresholdValue { get; set; }
        public string Condition { get; set; }
        public Dictionary<string, double> AdditionalThresholds { get; set; }

        public bool EvaluateCondition(MetricsSummary metrics)
        {
            // Implementation depends on the condition logic
            return false;
        }

        public double GetCurrentValue(MetricsSummary metrics)
        {
            // Implementation to extract current value based on metric name
            return 0.0;
        }

        public string GenerateAlertMessage(MetricsSummary metrics)
        {
            return $"Custom alert for {MetricName}: {Description}";
        }
    }

    public class PerformanceComparison
    {
        public TimeRange Period1 { get; set; }
        public TimeRange Period2 { get; set; }
        public MetricComparison CpuUsageComparison { get; set; }
        public MetricComparison MemoryUsageComparison { get; set; }
        public MetricComparison ThroughputComparison { get; set; }
        public MetricComparison LatencyComparison { get; set; }
        public ResourceEfficiencyDelta ResourceEfficiencyDelta { get; set; }
    }

    public class MetricComparison
    {
        public double Period1Average { get; set; }
        public double Period2Average { get; set; }
        public double PercentageChange { get; set; }
        public List<DataPoint> Period1Trend { get; set; }
        public List<DataPoint> Period2Trend { get; set; }
        public string Analysis { get; set; }
    }

    public class ResourceEfficiencyDelta
    {
        public double CpuEfficiencyChange { get; set; }
        public double MemoryEfficiencyChange { get; set; }
        public double ThroughputPerResourceUnit { get; set; }
        public List<string> ImprovementAreas { get; set; }
    }

    public class ResourceUsagePatterns
    {
        public TimeRange TimeRange { get; set; }
        public List<DailyPattern> DailyPatterns { get; set; }
        public List<WeeklyPattern> WeeklyPatterns { get; set; }
        public List<PeakUsagePeriod> PeakUsagePeriods { get; set; }
        public List<ResourceCorrelation> ResourceCorrelations { get; set; }
        public List<AnomalyPattern> AnomalyPatterns { get; set; }
    }

    public class BottleneckCorrelation
    {
        public TimeRange TimeRange { get; set; }
        public List<ResourceCorrelation> ResourceCorrelations { get; set; }
        public List<WorkloadCorrelation> WorkloadCorrelations { get; set; }
        public List<CascadingEffect> CascadingEffects { get; set; }
        public ImpactAnalysis ImpactAnalysis { get; set; }
    }

    public class CapacityPlanningInsights
    {
        public TimeRange TimeRange { get; set; }
        public GrowthTrends GrowthTrends { get; set; }
        public ResourceProjections ResourceProjections { get; set; }
        public List<BottleneckRisk> BottleneckRisks { get; set; }
        public List<ScalingRecommendation> ScalingRecommendations { get; set; }
        public List<OptimizationOpportunity> OptimizationOpportunities { get; set; }
    }

    public class HeatMapData
    {
        public TimeRange TimeRange { get; set; }
        public Dictionary<string, double[,]> ResourceIntensityMap { get; set; }
        public List<Hotspot> BottleneckHotspots { get; set; }
        public Dictionary<string, double[]> ComponentLoadDistribution { get; set; }
        public double[,] TimeBasedHeatMap { get; set; }
    }

    public class DailyPattern
    {
        public int HourOfDay { get; set; }
        public double AverageLoad { get; set; }
        public double PeakLoad { get; set; }
        public Dictionary<string, double> ResourceUtilization { get; set; }
    }

    public class WeeklyPattern
    {
        public DayOfWeek DayOfWeek { get; set; }
        public List<DailyPattern> HourlyPatterns { get; set; }
        public double AverageDailyLoad { get; set; }
    }

    public class PeakUsagePeriod
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, double> PeakMetrics { get; set; }
        public string Cause { get; set; }
    }

    public class ResourceCorrelation
    {
        public string Resource1 { get; set; }
        public string Resource2 { get; set; }
        public double CorrelationCoefficient { get; set; }
        public string CorrelationType { get; set; }
        public string Analysis { get; set; }
    }

    public class AnomalyPattern
    {
        public DateTime Timestamp { get; set; }
        public string MetricName { get; set; }
        public double ExpectedValue { get; set; }
        public double ActualValue { get; set; }
        public string AnomalyType { get; set; }
        public double Severity { get; set; }
    }

    public class WorkloadCorrelation
    {
        public string WorkloadType { get; set; }
        public Dictionary<string, double> ResourceImpact { get; set; }
        public List<string> AffectedComponents { get; set; }
        public double CorrelationStrength { get; set; }
    }

    public class CascadingEffect
    {
        public string InitialBottleneck { get; set; }
        public List<string> AffectedComponents { get; set; }
        public TimeSpan PropagationDelay { get; set; }
        public double ImpactSeverity { get; set; }
    }

    public class ImpactAnalysis
    {
        public Dictionary<string, double> ComponentImpactScores { get; set; }
        public List<string> CriticalPaths { get; set; }
        public Dictionary<string, TimeSpan> RecoveryTimes { get; set; }
        public List<string> MitigationStrategies { get; set; }
    }

    public class GrowthTrends
    {
        public double MonthlyGrowthRate { get; set; }
        public Dictionary<string, double> ResourceGrowthRates { get; set; }
        public List<DataPoint> HistoricalTrend { get; set; }
        public List<DataPoint> ProjectedTrend { get; set; }
    }

    public class ResourceProjections
    {
        public Dictionary<string, List<DataPoint>> ResourceForecasts { get; set; }
        public DateTime SaturationDate { get; set; }
        public Dictionary<string, double> ConfidenceIntervals { get; set; }
    }

    public class BottleneckRisk
    {
        public string Component { get; set; }
        public double RiskScore { get; set; }
        public DateTime PredictedOccurrence { get; set; }
        public List<string> ContributingFactors { get; set; }
        public List<string> PreventiveActions { get; set; }
    }

    public class ScalingRecommendation
    {
        public string Resource { get; set; }
        public double CurrentCapacity { get; set; }
        public double RecommendedCapacity { get; set; }
        public DateTime ImplementBy { get; set; }
        public string Justification { get; set; }
        public double ROIEstimate { get; set; }
    }

    public class OptimizationOpportunity
    {
        public string Area { get; set; }
        public string Description { get; set; }
        public double ExpectedImprovement { get; set; }
        public string Implementation { get; set; }
        public double EffortEstimate { get; set; }
    }

    public class Hotspot
    {
        public string Component { get; set; }
        public DateTime Timestamp { get; set; }
        public double Intensity { get; set; }
        public Dictionary<string, double> ResourceMetrics { get; set; }
        public List<string> ContributingFactors { get; set; }
    }

    public class DataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }
}