using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ValidationPipelineDashboard
    {
        private readonly MetricsAggregator _metricsAggregator;
        private readonly ResourceAlertManager _alertManager;
        private readonly ResourceAnalytics _resourceAnalytics;
        private readonly MemoryAnalyticsDashboard _memoryAnalytics;
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly TimeSeriesStorage _timeSeriesStorage;

        public ValidationPipelineDashboard(
            MetricsAggregator metricsAggregator,
            ResourceAlertManager alertManager,
            ResourceAnalytics resourceAnalytics,
            MemoryAnalyticsDashboard memoryAnalytics,
            ResourceTrendAnalyzer trendAnalyzer,
            TimeSeriesStorage timeSeriesStorage)
        {
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
            _resourceAnalytics = resourceAnalytics;
            _memoryAnalytics = memoryAnalytics;
            _trendAnalyzer = trendAnalyzer;
            _timeSeriesStorage = timeSeriesStorage;
        }

        public async Task<PipelinePerformanceMetrics> GetRealtimeMetricsAsync()
        {
            var metrics = await _metricsAggregator.GetCurrentMetricsAsync();
            return new PipelinePerformanceMetrics
            {
                Throughput = metrics.Throughput,
                Latency = metrics.Latency,
                ActiveValidations = metrics.ActiveValidations,
                QueueDepth = metrics.QueueDepth,
                ResourceUtilization = await _resourceAnalytics.GetCurrentUtilizationAsync(),
                CircuitBreakerStates = await GetCircuitBreakerStatesAsync()
            };
        }

        public async Task<HistoricalAnalysisReport> GetHistoricalAnalysisAsync(DateTime startTime, DateTime endTime)
        {
            var performanceTrends = await _timeSeriesStorage.GetMetricsAsync(startTime, endTime);
            var resourcePatterns = await _resourceAnalytics.GetHistoricalPatternsAsync(startTime, endTime);
            var failureAnalysis = await _metricsAggregator.GetFailureAnalysisAsync(startTime, endTime);

            return new HistoricalAnalysisReport
            {
                PerformanceTrends = performanceTrends,
                ResourcePatterns = resourcePatterns,
                FailureAnalysis = failureAnalysis,
                MiddlewarePerformance = await GetMiddlewarePerformanceAsync(startTime, endTime),
                ThrottlingEffectiveness = await _trendAnalyzer.AnalyzeThrottlingEffectivenessAsync(startTime, endTime)
            };
        }

        public async Task<AlertDashboard> GetAlertDashboardAsync()
        {
            return new AlertDashboard
            {
                CurrentAlerts = await _alertManager.GetActiveAlertsAsync(),
                HistoricalAlerts = await _alertManager.GetHistoricalAlertsAsync(),
                MetricCorrelations = await _alertManager.GetAlertMetricCorrelationsAsync(),
                ThresholdConfigurations = _alertManager.GetAlertThresholds(),
                TrendAnalysis = await _alertManager.GetAlertTrendsAsync()
            };
        }

        public async Task<OperationalInsights> GetOperationalInsightsAsync()
        {
            return new OperationalInsights
            {
                BottleneckAnalysis = await _resourceAnalytics.IdentifyBottlenecksAsync(),
                OptimizationRecommendations = await _resourceAnalytics.GetOptimizationRecommendationsAsync(),
                ConfigurationEffectiveness = await _trendAnalyzer.AnalyzeConfigurationEffectivenessAsync(),
                PerformanceImpactAnalysis = await _trendAnalyzer.AnalyzeConfigurationImpactAsync()
            };
        }

        private async Task<Dictionary<string, CircuitBreakerState>> GetCircuitBreakerStatesAsync()
        {
            return await _metricsAggregator.GetCircuitBreakerStatesAsync();
        }

        private async Task<Dictionary<string, MiddlewarePerformanceMetrics>> GetMiddlewarePerformanceAsync(
            DateTime startTime, DateTime endTime)
        {
            return await _metricsAggregator.GetMiddlewarePerformanceAsync(startTime, endTime);
        }
    }

    public class PipelinePerformanceMetrics
    {
        public double Throughput { get; set; }
        public TimeSpan Latency { get; set; }
        public int ActiveValidations { get; set; }
        public int QueueDepth { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public Dictionary<string, CircuitBreakerState> CircuitBreakerStates { get; set; }
    }

    public class HistoricalAnalysisReport
    {
        public List<TimeSeriesMetric> PerformanceTrends { get; set; }
        public ResourceUsagePatterns ResourcePatterns { get; set; }
        public FailureAnalysis FailureAnalysis { get; set; }
        public Dictionary<string, MiddlewarePerformanceMetrics> MiddlewarePerformance { get; set; }
        public ThrottlingEffectivenessReport ThrottlingEffectiveness { get; set; }
    }

    public class AlertDashboard
    {
        public List<Alert> CurrentAlerts { get; set; }
        public List<Alert> HistoricalAlerts { get; set; }
        public Dictionary<string, List<MetricCorrelation>> MetricCorrelations { get; set; }
        public Dictionary<string, AlertThreshold> ThresholdConfigurations { get; set; }
        public AlertTrendAnalysis TrendAnalysis { get; set; }
    }

    public class OperationalInsights
    {
        public List<BottleneckAnalysis> BottleneckAnalysis { get; set; }
        public List<OptimizationRecommendation> OptimizationRecommendations { get; set; }
        public ConfigurationEffectivenessMetrics ConfigurationEffectiveness { get; set; }
        public List<ConfigurationImpactAnalysis> PerformanceImpactAnalysis { get; set; }
    }
}