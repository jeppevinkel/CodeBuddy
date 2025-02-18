using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    /// <summary>
    /// Provides a comprehensive dashboard for monitoring validation middleware performance,
    /// resource usage, and health metrics.
    /// </summary>
    public class ValidationPipelineDashboard
    {
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly IResourceAlertManager _alertManager;
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly IMemoryAnalyticsDashboard _memoryAnalytics;
        private readonly IResourceTrendAnalyzer _trendAnalyzer;
        private readonly ITimeSeriesStorage _timeSeriesStorage;
        private readonly IValidatorRegistry _validatorRegistry;

        public ValidationPipelineDashboard(
            IMetricsAggregator metricsAggregator,
            IResourceAlertManager alertManager,
            IResourceAnalytics resourceAnalytics,
            IMemoryAnalyticsDashboard memoryAnalytics,
            IResourceTrendAnalyzer trendAnalyzer,
            ITimeSeriesStorage timeSeriesStorage,
            IValidatorRegistry validatorRegistry)
        {
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
            _resourceAnalytics = resourceAnalytics;
            _memoryAnalytics = memoryAnalytics;
            _trendAnalyzer = trendAnalyzer;
            _timeSeriesStorage = timeSeriesStorage;
        }

        /// <summary>
        /// Gets real-time metrics for all validation middleware components.
        /// </summary>
        /// <returns>Comprehensive dashboard metrics including performance, health, and resource utilization.</returns>
        public async Task<ValidationDashboardSummary> GetRealtimeMetricsAsync()
        {
            var middlewares = _validatorRegistry.GetAllMiddleware();
            var dashboardSummary = new ValidationDashboardSummary
            {
                Timestamp = DateTime.UtcNow,
                MiddlewareMetrics = new Dictionary<string, ValidationMiddlewareMetrics>(),
                SystemMetrics = await GetSystemWideMetricsAsync(),
                SystemAlerts = await _alertManager.GetActiveAlertsAsync(),
                Trends = await GetHistoricalTrendsAsync()
            };

            foreach (var middleware in middlewares)
            {
                var metrics = await GetMiddlewareMetricsAsync(middleware);
                dashboardSummary.MiddlewareMetrics[middleware.Name] = metrics;
            }

            return dashboardSummary;
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

        private async Task<ValidationMiddlewareMetrics> GetMiddlewareMetricsAsync(IValidationMiddleware middleware)
        {
            var executionMetrics = await _metricsAggregator.GetMiddlewareExecutionMetricsAsync(middleware.Name);
            var resourceMetrics = await _resourceAnalytics.GetMiddlewareResourceMetricsAsync(middleware.Name);
            var circuitBreakerMetrics = await _metricsAggregator.GetCircuitBreakerMetricsAsync(middleware.Name);
            var retryMetrics = await _metricsAggregator.GetRetryMetricsAsync(middleware.Name);

            return new ValidationMiddlewareMetrics
            {
                MiddlewareName = middleware.Name,
                SuccessMetrics = new SuccessFailureMetrics
                {
                    TotalRequests = executionMetrics.TotalRequests,
                    SuccessfulRequests = executionMetrics.SuccessfulRequests,
                    FailedRequests = executionMetrics.FailedRequests,
                    TopFailureCategories = await _metricsAggregator.GetTopFailureCategoriesAsync(middleware.Name)
                },
                PerformanceMetrics = new PerformanceMetrics
                {
                    AverageExecutionTime = executionMetrics.AverageExecutionTime,
                    P95ExecutionTime = executionMetrics.P95ExecutionTime,
                    P99ExecutionTime = executionMetrics.P99ExecutionTime,
                    RequestsPerSecond = executionMetrics.RequestsPerSecond,
                    ConcurrentExecutions = executionMetrics.ConcurrentExecutions
                },
                CircuitBreakerMetrics = circuitBreakerMetrics,
                RetryMetrics = retryMetrics,
                ResourceMetrics = resourceMetrics,
                ActiveAlerts = await _alertManager.GetActiveAlertsForMiddlewareAsync(middleware.Name)
            };
        }

        private async Task<SystemWideMetrics> GetSystemWideMetricsAsync()
        {
            var overallMetrics = await _metricsAggregator.GetSystemWideMetricsAsync();
            var resourceMetrics = await _resourceAnalytics.GetSystemResourceMetricsAsync();
            var circuitBreakerStates = await _metricsAggregator.GetCircuitBreakerStatesAsync();

            return new SystemWideMetrics
            {
                OverallSuccessRate = overallMetrics.SuccessRate,
                AverageResponseTime = overallMetrics.AverageResponseTime.TotalMilliseconds,
                TotalActiveValidations = overallMetrics.ActiveValidations,
                TotalResourceUtilization = resourceMetrics,
                ActiveCircuitBreakers = circuitBreakerStates.Count(x => x.Value.State == CircuitBreakerState.Open),
                TotalAlerts = (await _alertManager.GetActiveAlertsAsync()).Count
            };
        }

        private async Task<HistoricalTrends> GetHistoricalTrendsAsync()
        {
            var timeRange = TimeSpan.FromHours(24);
            var endTime = DateTime.UtcNow;
            var startTime = endTime.Subtract(timeRange);

            return new HistoricalTrends
            {
                SuccessRates = await _timeSeriesStorage.GetMetricTimeSeriesAsync("success_rate", startTime, endTime),
                ResponseTimes = await _timeSeriesStorage.GetMetricTimeSeriesAsync("response_time", startTime, endTime),
                ResourceUtilization = await _timeSeriesStorage.GetMetricTimeSeriesAsync("resource_utilization", startTime, endTime),
                ThroughputRates = await _timeSeriesStorage.GetMetricTimeSeriesAsync("throughput", startTime, endTime)
            };
        }

        public async Task<ExportableReport> GenerateExportableReportAsync(DateTime startTime, DateTime endTime)
        {
            var report = await GetHistoricalAnalysisAsync(startTime, endTime);
            var insights = await GetOperationalInsightsAsync();
            
            return new ExportableReport
            {
                TimeRange = new TimeRange { Start = startTime, End = endTime },
                PerformanceMetrics = report.PerformanceTrends,
                ResourceUtilization = report.ResourcePatterns,
                FailureAnalysis = report.FailureAnalysis,
                MiddlewarePerformance = report.MiddlewarePerformance,
                Recommendations = insights.OptimizationRecommendations,
                Bottlenecks = insights.BottleneckAnalysis,
                AlertHistory = await _alertManager.GetHistoricalAlertsAsync(startTime, endTime)
            };
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