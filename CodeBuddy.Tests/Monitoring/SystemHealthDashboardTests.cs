using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.Monitoring;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.Monitoring
{
    public class SystemHealthDashboardTests
    {
        private readonly Mock<ILogger<SystemHealthDashboard>> _loggerMock;
        private readonly Mock<MetricsDashboard> _metricsDashboardMock;
        private readonly Mock<ResourceMonitoringDashboard> _resourceDashboardMock;
        private readonly Mock<ValidationPipelineDashboard> _pipelineDashboardMock;
        private readonly Mock<MemoryAnalyticsDashboard> _memoryDashboardMock;
        private readonly Mock<PluginHealthMonitor> _pluginMonitorMock;

        public SystemHealthDashboardTests()
        {
            _loggerMock = new Mock<ILogger<SystemHealthDashboard>>();
            _metricsDashboardMock = new Mock<MetricsDashboard>();
            _resourceDashboardMock = new Mock<ResourceMonitoringDashboard>();
            _pipelineDashboardMock = new Mock<ValidationPipelineDashboard>();
            _memoryDashboardMock = new Mock<MemoryAnalyticsDashboard>();
            _pluginMonitorMock = new Mock<PluginHealthMonitor>();
        }

        [Fact]
        public async Task GetSystemHealthSnapshot_ReturnsHealthyStatus_WhenAllMetricsAreNormal()
        {
            // Arrange
            SetupHealthyMocks();
            var dashboard = CreateDashboard();

            // Act
            var snapshot = await dashboard.GetSystemHealthSnapshotAsync();

            // Assert
            Assert.Equal(SystemStatus.Healthy, snapshot.Status);
            Assert.NotNull(snapshot.ResourceMetrics);
            Assert.NotNull(snapshot.ValidationPipelineMetrics);
            Assert.NotNull(snapshot.MemoryMetrics);
            Assert.NotNull(snapshot.PluginMetrics);
            Assert.NotNull(snapshot.CacheMetrics);
        }

        [Fact]
        public async Task GetSystemHealthSnapshot_ReturnsWarningStatus_WhenMetricsShowWarnings()
        {
            // Arrange
            SetupWarningMocks();
            var dashboard = CreateDashboard();

            // Act
            var snapshot = await dashboard.GetSystemHealthSnapshotAsync();

            // Assert
            Assert.Equal(SystemStatus.Warning, snapshot.Status);
            Assert.True(snapshot.ResourceMetrics.CpuUtilization >= 70);
            Assert.True(snapshot.ValidationPipelineMetrics.SuccessRate <= 0.9);
        }

        [Fact]
        public async Task GetSystemHealthSnapshot_ReturnsCriticalStatus_WhenMetricsShowCriticalIssues()
        {
            // Arrange
            SetupCriticalMocks();
            var dashboard = CreateDashboard();

            // Act
            var snapshot = await dashboard.GetSystemHealthSnapshotAsync();

            // Assert
            Assert.Equal(SystemStatus.Critical, snapshot.Status);
            Assert.True(snapshot.ResourceMetrics.CpuUtilization >= 90);
            Assert.True(snapshot.ValidationPipelineMetrics.SuccessRate <= 0.8);
        }

        [Fact]
        public async Task GetHistoricalHealthData_ReturnsCompleteHistoricalData()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-1);
            var endTime = DateTime.UtcNow;
            SetupHistoricalMocks(startTime, endTime);
            var dashboard = CreateDashboard();

            // Act
            var history = await dashboard.GetHistoricalHealthDataAsync(startTime, endTime);

            // Assert
            Assert.NotNull(history);
            Assert.Equal(startTime, history.TimeRange.Start);
            Assert.Equal(endTime, history.TimeRange.End);
            Assert.NotNull(history.ResourceTrends);
            Assert.NotNull(history.PipelinePerformance);
            Assert.NotNull(history.MemoryTrends);
            Assert.NotNull(history.PluginHealth);
            Assert.NotNull(history.Alerts);
            Assert.NotNull(history.Insights);
        }

        private void SetupHealthyMocks()
        {
            _resourceDashboardMock.Setup(x => x.GetCurrentMetricsAsync())
                .ReturnsAsync(new ResourceMetricsModel
                {
                    CpuUsagePercent = 50,
                    MemoryUsageBytes = 4L * 1024 * 1024 * 1024, // 4GB
                    ActiveHandles = 1000
                });

            _pipelineDashboardMock.Setup(x => x.GetRealtimeMetricsAsync())
                .ReturnsAsync(new ValidationDashboardSummary
                {
                    SystemMetrics = new SystemWideMetrics
                    {
                        OverallSuccessRate = 0.95,
                        AverageResponseTime = 100
                    }
                });

            _pluginMonitorMock.Setup(x => x.GetPluginHealthStatusAsync())
                .ReturnsAsync(new PluginHealthStatus
                {
                    ActivePlugins = 10,
                    FailedPlugins = 0,
                    TotalPlugins = 10
                });
        }

        private void SetupWarningMocks()
        {
            _resourceDashboardMock.Setup(x => x.GetCurrentMetricsAsync())
                .ReturnsAsync(new ResourceMetricsModel
                {
                    CpuUsagePercent = 75,
                    MemoryUsageBytes = 6L * 1024 * 1024 * 1024, // 6GB
                    ActiveHandles = 2000
                });

            _pipelineDashboardMock.Setup(x => x.GetRealtimeMetricsAsync())
                .ReturnsAsync(new ValidationDashboardSummary
                {
                    SystemMetrics = new SystemWideMetrics
                    {
                        OverallSuccessRate = 0.85,
                        AverageResponseTime = 200
                    }
                });

            _pluginMonitorMock.Setup(x => x.GetPluginHealthStatusAsync())
                .ReturnsAsync(new PluginHealthStatus
                {
                    ActivePlugins = 9,
                    FailedPlugins = 1,
                    TotalPlugins = 10
                });
        }

        private void SetupCriticalMocks()
        {
            _resourceDashboardMock.Setup(x => x.GetCurrentMetricsAsync())
                .ReturnsAsync(new ResourceMetricsModel
                {
                    CpuUsagePercent = 95,
                    MemoryUsageBytes = 7L * 1024 * 1024 * 1024, // 7GB
                    ActiveHandles = 3000
                });

            _pipelineDashboardMock.Setup(x => x.GetRealtimeMetricsAsync())
                .ReturnsAsync(new ValidationDashboardSummary
                {
                    SystemMetrics = new SystemWideMetrics
                    {
                        OverallSuccessRate = 0.75,
                        AverageResponseTime = 500
                    }
                });

            _pluginMonitorMock.Setup(x => x.GetPluginHealthStatusAsync())
                .ReturnsAsync(new PluginHealthStatus
                {
                    ActivePlugins = 7,
                    FailedPlugins = 3,
                    TotalPlugins = 10
                });
        }

        private void SetupHistoricalMocks(DateTime startTime, DateTime endTime)
        {
            _pipelineDashboardMock.Setup(x => x.GetHistoricalAnalysisAsync(startTime, endTime))
                .ReturnsAsync(new HistoricalAnalysisReport());

            _resourceDashboardMock.Setup(x => x.GetTrendData(It.IsAny<TimeSpan>()))
                .Returns(new ResourceTrendData());

            _memoryDashboardMock.Setup(x => x.GetMemoryTrendsAsync(startTime, endTime))
                .ReturnsAsync(new List<MemoryTrendData>());

            _pluginMonitorMock.Setup(x => x.GetPluginHealthHistoryAsync(startTime, endTime))
                .ReturnsAsync(new List<PluginHealthSnapshot>());

            _pipelineDashboardMock.Setup(x => x.GetAlertDashboardAsync())
                .ReturnsAsync(new AlertDashboard());

            _pipelineDashboardMock.Setup(x => x.GetOperationalInsightsAsync())
                .ReturnsAsync(new OperationalInsights());
        }

        private SystemHealthDashboard CreateDashboard()
        {
            return new SystemHealthDashboard(
                _loggerMock.Object,
                _metricsDashboardMock.Object,
                _resourceDashboardMock.Object,
                _pipelineDashboardMock.Object,
                _memoryDashboardMock.Object,
                _pluginMonitorMock.Object);
        }
    }
}