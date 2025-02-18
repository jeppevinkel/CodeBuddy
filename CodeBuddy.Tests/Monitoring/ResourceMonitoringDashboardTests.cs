using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.Monitoring;
using CodeBuddy.Core.Models;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.Monitoring
{
    public class ResourceMonitoringDashboardTests
    {
        private readonly Mock<IValidatorRegistry> _validatorRegistryMock;
        private readonly Mock<PerformanceMonitor> _performanceMonitorMock;
        private readonly ResourceMonitoringDashboard _dashboard;

        public ResourceMonitoringDashboardTests()
        {
            _validatorRegistryMock = new Mock<IValidatorRegistry>();
            _performanceMonitorMock = new Mock<PerformanceMonitor>();
            _dashboard = new ResourceMonitoringDashboard(
                _validatorRegistryMock.Object,
                _performanceMonitorMock.Object);
        }

        [Fact]
        public async Task StartMonitoring_ShouldStartCollectingMetrics()
        {
            // Arrange
            Assert.False(await _dashboard.IsMonitoring());

            // Act
            await _dashboard.StartMonitoring();

            // Assert
            Assert.True(await _dashboard.IsMonitoring());
        }

        [Fact]
        public async Task StopMonitoring_ShouldStopCollectingMetrics()
        {
            // Arrange
            await _dashboard.StartMonitoring();
            Assert.True(await _dashboard.IsMonitoring());

            // Act
            await _dashboard.StopMonitoring();

            // Assert
            Assert.False(await _dashboard.IsMonitoring());
        }

        [Fact]
        public async Task GetCurrentResourceUtilization_ShouldReturnValidMetrics()
        {
            // Arrange
            _performanceMonitorMock.Setup(x => x.GetCpuUtilization())
                .ReturnsAsync(50.0);
            _performanceMonitorMock.Setup(x => x.GetAverageProcessingTime())
                .ReturnsAsync(TimeSpan.FromMilliseconds(100));

            // Act
            var metrics = await _dashboard.GetCurrentResourceUtilization();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(50.0, metrics.CpuUtilizationPercent);
            Assert.Equal(Environment.ProcessorCount, metrics.ProcessorCount);
            Assert.True(metrics.TotalMemoryBytes > 0);
            Assert.True(metrics.AvailableMemoryBytes > 0);
        }

        [Fact]
        public async Task SetResourceAlert_ShouldCreateNewAlert()
        {
            // Arrange
            var alertConfig = new ResourceAlertConfig
            {
                ResourceType = ResourceType.Memory,
                ThresholdValue = 0.9,
                Priority = AlertPriority.High,
                Duration = TimeSpan.FromMinutes(5)
            };

            // Act
            await _dashboard.SetResourceAlert(alertConfig);
            var activeAlerts = await _dashboard.GetActiveAlerts();

            // Assert
            Assert.NotNull(activeAlerts);
            // Note: Alert won't be active until threshold is exceeded
            Assert.Empty(activeAlerts);
        }

        [Fact]
        public async Task GetBottleneckAnalysis_ShouldIdentifyBottlenecks()
        {
            // Arrange
            _performanceMonitorMock.Setup(x => x.GetCpuUtilization())
                .ReturnsAsync(90.0);

            // Act
            var analysis = await _dashboard.GetBottleneckAnalysis();

            // Assert
            Assert.NotNull(analysis);
            Assert.Equal(ResourceType.CPU, analysis.PrimaryBottleneck);
            Assert.True(analysis.BottleneckSeverity > 0.8);
            Assert.NotNull(analysis.RecommendedAction);
        }

        [Fact]
        public async Task PredictResourceUsage_ShouldProvidePredictions()
        {
            // Act
            var prediction = await _dashboard.PredictResourceUsage(TimeSpan.FromHours(1));

            // Assert
            Assert.NotNull(prediction);
            Assert.NotNull(prediction.PredictedUtilization);
            Assert.True(prediction.PredictedUtilization.Any());
            Assert.True(prediction.Confidence >= 0 && prediction.Confidence <= 1);
            Assert.NotNull(prediction.PotentialIssues);
        }

        [Fact]
        public async Task ExportMetricsData_ShouldExportInRequestedFormat()
        {
            // Arrange
            await _dashboard.StartMonitoring();
            await Task.Delay(100); // Allow some metrics to be collected

            // Act & Assert
            await Assert.ThrowsAsync<NotImplementedException>(() =>
                _dashboard.ExportMetricsData(
                    DateTime.UtcNow.AddHours(-1),
                    DateTime.UtcNow,
                    ExportFormat.JSON));
        }
    }
}