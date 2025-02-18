using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ValidationPipelineDashboardTests
    {
        private readonly Mock<MetricsAggregator> _metricsAggregatorMock;
        private readonly Mock<ResourceAlertManager> _alertManagerMock;
        private readonly Mock<ResourceAnalytics> _resourceAnalyticsMock;
        private readonly Mock<MemoryAnalyticsDashboard> _memoryAnalyticsMock;
        private readonly Mock<ResourceTrendAnalyzer> _trendAnalyzerMock;
        private readonly Mock<TimeSeriesStorage> _timeSeriesStorageMock;
        private readonly ValidationPipelineDashboard _dashboard;

        public ValidationPipelineDashboardTests()
        {
            _metricsAggregatorMock = new Mock<MetricsAggregator>();
            _alertManagerMock = new Mock<ResourceAlertManager>();
            _resourceAnalyticsMock = new Mock<ResourceAnalytics>();
            _memoryAnalyticsMock = new Mock<MemoryAnalyticsDashboard>();
            _trendAnalyzerMock = new Mock<ResourceTrendAnalyzer>();
            _timeSeriesStorageMock = new Mock<TimeSeriesStorage>();

            _dashboard = new ValidationPipelineDashboard(
                _metricsAggregatorMock.Object,
                _alertManagerMock.Object,
                _resourceAnalyticsMock.Object,
                _memoryAnalyticsMock.Object,
                _trendAnalyzerMock.Object,
                _timeSeriesStorageMock.Object
            );
        }

        [Fact]
        public async Task GetRealtimeMetrics_ShouldReturnCurrentMetrics()
        {
            // Arrange
            var expectedMetrics = new PipelinePerformanceMetrics
            {
                Throughput = 100.0,
                Latency = TimeSpan.FromMilliseconds(50),
                ActiveValidations = 10,
                QueueDepth = 5
            };

            _metricsAggregatorMock.Setup(m => m.GetCurrentMetricsAsync())
                .ReturnsAsync(new
                {
                    Throughput = expectedMetrics.Throughput,
                    Latency = expectedMetrics.Latency,
                    ActiveValidations = expectedMetrics.ActiveValidations,
                    QueueDepth = expectedMetrics.QueueDepth
                });

            // Act
            var result = await _dashboard.GetRealtimeMetricsAsync();

            // Assert
            Assert.Equal(expectedMetrics.Throughput, result.Throughput);
            Assert.Equal(expectedMetrics.Latency, result.Latency);
            Assert.Equal(expectedMetrics.ActiveValidations, result.ActiveValidations);
            Assert.Equal(expectedMetrics.QueueDepth, result.QueueDepth);
        }

        [Fact]
        public async Task GetHistoricalAnalysis_ShouldReturnCompleteAnalysisReport()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-7);
            var endTime = DateTime.UtcNow;

            var expectedTrends = new List<TimeSeriesMetric>();
            var expectedPatterns = new ResourceUsagePatterns();
            var expectedFailures = new FailureAnalysis();

            _timeSeriesStorageMock.Setup(t => t.GetMetricsAsync(startTime, endTime))
                .ReturnsAsync(expectedTrends);
            _resourceAnalyticsMock.Setup(r => r.GetHistoricalPatternsAsync(startTime, endTime))
                .ReturnsAsync(expectedPatterns);
            _metricsAggregatorMock.Setup(m => m.GetFailureAnalysisAsync(startTime, endTime))
                .ReturnsAsync(expectedFailures);

            // Act
            var result = await _dashboard.GetHistoricalAnalysisAsync(startTime, endTime);

            // Assert
            Assert.Same(expectedTrends, result.PerformanceTrends);
            Assert.Same(expectedPatterns, result.ResourcePatterns);
            Assert.Same(expectedFailures, result.FailureAnalysis);
        }

        [Fact]
        public async Task GetAlertDashboard_ShouldReturnCompleteAlertInformation()
        {
            // Arrange
            var currentAlerts = new List<Alert>();
            var historicalAlerts = new List<Alert>();
            var correlations = new Dictionary<string, List<MetricCorrelation>>();
            var thresholds = new Dictionary<string, AlertThreshold>();
            var trends = new AlertTrendAnalysis();

            _alertManagerMock.Setup(a => a.GetActiveAlertsAsync()).ReturnsAsync(currentAlerts);
            _alertManagerMock.Setup(a => a.GetHistoricalAlertsAsync()).ReturnsAsync(historicalAlerts);
            _alertManagerMock.Setup(a => a.GetAlertMetricCorrelationsAsync()).ReturnsAsync(correlations);
            _alertManagerMock.Setup(a => a.GetAlertThresholds()).Returns(thresholds);
            _alertManagerMock.Setup(a => a.GetAlertTrendsAsync()).ReturnsAsync(trends);

            // Act
            var result = await _dashboard.GetAlertDashboardAsync();

            // Assert
            Assert.Same(currentAlerts, result.CurrentAlerts);
            Assert.Same(historicalAlerts, result.HistoricalAlerts);
            Assert.Same(correlations, result.MetricCorrelations);
            Assert.Same(thresholds, result.ThresholdConfigurations);
            Assert.Same(trends, result.TrendAnalysis);
        }

        [Fact]
        public async Task GetOperationalInsights_ShouldReturnComprehensiveInsights()
        {
            // Arrange
            var bottlenecks = new List<BottleneckAnalysis>();
            var recommendations = new List<OptimizationRecommendation>();
            var effectiveness = new ConfigurationEffectivenessMetrics();
            var impact = new List<ConfigurationImpactAnalysis>();

            _resourceAnalyticsMock.Setup(r => r.IdentifyBottlenecksAsync()).ReturnsAsync(bottlenecks);
            _resourceAnalyticsMock.Setup(r => r.GetOptimizationRecommendationsAsync()).ReturnsAsync(recommendations);
            _trendAnalyzerMock.Setup(t => t.AnalyzeConfigurationEffectivenessAsync()).ReturnsAsync(effectiveness);
            _trendAnalyzerMock.Setup(t => t.AnalyzeConfigurationImpactAsync()).ReturnsAsync(impact);

            // Act
            var result = await _dashboard.GetOperationalInsightsAsync();

            // Assert
            Assert.Same(bottlenecks, result.BottleneckAnalysis);
            Assert.Same(recommendations, result.OptimizationRecommendations);
            Assert.Same(effectiveness, result.ConfigurationEffectiveness);
            Assert.Same(impact, result.PerformanceImpactAnalysis);
        }
    }
}