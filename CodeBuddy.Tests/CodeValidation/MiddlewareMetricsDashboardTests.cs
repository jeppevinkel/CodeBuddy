using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class MiddlewareMetricsDashboardTests
    {
        private readonly Mock<IMetricsAggregator> _metricsAggregator;
        private readonly Mock<ResourceAlertManager> _alertManager;
        private readonly Mock<TimeSeriesStorage> _timeSeriesStorage;
        private readonly MiddlewareMetricsDashboard _dashboard;

        public MiddlewareMetricsDashboardTests()
        {
            _metricsAggregator = new Mock<IMetricsAggregator>();
            _alertManager = new Mock<ResourceAlertManager>();
            _timeSeriesStorage = new Mock<TimeSeriesStorage>();
            _dashboard = new MiddlewareMetricsDashboard(
                _metricsAggregator.Object,
                _alertManager.Object,
                _timeSeriesStorage.Object);
        }

        [Fact]
        public async Task RecordMiddlewareExecution_ShouldStoreMetrics()
        {
            // Arrange
            var middlewareId = "test-middleware";
            var executionTime = TimeSpan.FromMilliseconds(100);
            var success = true;
            var retryCount = 0;

            // Act
            await _dashboard.RecordMiddlewareExecution(middlewareId, executionTime, success, retryCount);

            // Assert
            _timeSeriesStorage.Verify(x => x.StoreMetrics(
                middlewareId,
                It.Is<Dictionary<string, double>>(d =>
                    d["execution_time"] == 100 &&
                    d["success"] == 1 &&
                    d["retry_count"] == 0)), Times.Once);
        }

        [Fact]
        public async Task UpdateCircuitBreakerStatus_ShouldRaiseAlert_WhenStatusChanges()
        {
            // Arrange
            var middlewareId = "test-middleware";

            // Act
            await _dashboard.UpdateCircuitBreakerStatus(middlewareId, true);

            // Assert
            _alertManager.Verify(x => x.RaiseAlert(
                It.Is<AlertModels.Alert>(a =>
                    a.Severity == AlertModels.AlertSeverity.Warning &&
                    a.Source == middlewareId &&
                    a.Message.Contains("open"))), Times.Once);
        }

        [Fact]
        public async Task UpdateResourceMetrics_ShouldAggregateMetrics()
        {
            // Arrange
            var middlewareId = "test-middleware";
            var metrics = new ResourceMetrics
            {
                CpuUsage = 50.0,
                MemoryUsage = 1024 * 1024 * 100, // 100 MB
                ThreadCount = 5
            };

            // Act
            await _dashboard.UpdateResourceMetrics(middlewareId, metrics);

            // Assert
            _metricsAggregator.Verify(x => x.AggregateMetrics(middlewareId, metrics), Times.Once);
        }

        [Fact]
        public void GetDashboardMetrics_ShouldReturnAggregatedMetrics()
        {
            // Arrange
            var middlewareId = "test-middleware";
            var executionTime = TimeSpan.FromMilliseconds(100);
            _dashboard.RecordMiddlewareExecution(middlewareId, executionTime, true, 0).Wait();

            // Act
            var metrics = _dashboard.GetDashboardMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.Contains(middlewareId, metrics.Keys);
            Assert.Equal(executionTime, metrics[middlewareId].AverageExecutionTime);
            Assert.Equal(1.0, metrics[middlewareId].SuccessRate);
            Assert.Equal(0, metrics[middlewareId].AverageRetryCount);
        }

        [Fact]
        public async Task GetHistoricalMetrics_ShouldReturnTimeSeriesData()
        {
            // Arrange
            var middlewareId = "test-middleware";
            var start = DateTime.UtcNow.AddHours(-1);
            var end = DateTime.UtcNow;
            var expectedMetrics = new Dictionary<string, List<HistoricalMetrics>>();

            _timeSeriesStorage.Setup(x => x.GetMetrics(middlewareId, start, end))
                             .ReturnsAsync(expectedMetrics);

            // Act
            var metrics = await _dashboard.GetHistoricalMetrics(middlewareId, start, end);

            // Assert
            Assert.Same(expectedMetrics, metrics);
            _timeSeriesStorage.Verify(x => x.GetMetrics(middlewareId, start, end), Times.Once);
        }

        [Fact]
        public async Task RecordMiddlewareExecution_ShouldRaiseAlert_WhenThresholdsExceeded()
        {
            // Arrange
            var middlewareId = "test-middleware";
            var executionTime = TimeSpan.FromMilliseconds(2000); // 2 seconds
            
            // Act
            for (int i = 0; i < 10; i++)
            {
                await _dashboard.RecordMiddlewareExecution(middlewareId, executionTime, false, 0);
            }

            // Assert
            _alertManager.Verify(x => x.RaiseAlert(
                It.Is<AlertModels.Alert>(a =>
                    a.Severity == AlertModels.AlertSeverity.Warning &&
                    a.Source == middlewareId &&
                    a.Message.Contains("High average execution time"))), Times.AtLeastOnce);

            _alertManager.Verify(x => x.RaiseAlert(
                It.Is<AlertModels.Alert>(a =>
                    a.Severity == AlertModels.AlertSeverity.Error &&
                    a.Source == middlewareId &&
                    a.Message.Contains("Low success rate"))), Times.AtLeastOnce);
        }
    }
}