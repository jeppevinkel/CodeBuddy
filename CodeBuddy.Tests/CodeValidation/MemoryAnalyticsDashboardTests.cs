using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class MemoryAnalyticsDashboardTests
    {
        private readonly Mock<MemoryLeakDetector> _mockMemoryLeakDetector;
        private readonly Mock<TimeSeriesStorage> _mockTimeSeriesStorage;
        private readonly ValidationResilienceConfig _config;
        private readonly MemoryAnalyticsConfig _analyticsConfig;
        private readonly MemoryAnalyticsDashboard _dashboard;

        public MemoryAnalyticsDashboardTests()
        {
            _mockMemoryLeakDetector = new Mock<MemoryLeakDetector>();
            _mockTimeSeriesStorage = new Mock<TimeSeriesStorage>();
            _config = new ValidationResilienceConfig();
            _analyticsConfig = new MemoryAnalyticsConfig
            {
                SamplingIntervalMs = 1000,
                LeakConfidenceThreshold = 0.8,
                MemoryThresholdBytes = 1024 * 1024 * 100, // 100 MB
                EnableAutomaticDumps = true
            };

            _dashboard = new MemoryAnalyticsDashboard(
                _mockMemoryLeakDetector.Object,
                _mockTimeSeriesStorage.Object,
                _config,
                _analyticsConfig);
        }

        [Fact]
        public async Task GenerateReport_ShouldIncludeAllRequiredComponents()
        {
            // Arrange
            var componentId = "test-component";
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;
            
            var mockMemoryMetrics = new List<MemoryMetrics>
            {
                new MemoryMetrics
                {
                    Timestamp = startTime,
                    TotalMemoryBytes = 1000000,
                    LargeObjectHeapBytes = 200000,
                    SmallObjectHeapBytes = 800000
                },
                new MemoryMetrics
                {
                    Timestamp = endTime,
                    TotalMemoryBytes = 1200000,
                    LargeObjectHeapBytes = 300000,
                    SmallObjectHeapBytes = 900000
                }
            };

            var mockAnalysis = new MemoryLeakAnalysis
            {
                ComponentId = componentId,
                LeakDetected = true,
                ConfidenceLevel = 95,
                AdditionalMetrics = new Dictionary<string, string>
                {
                    { "LeakType_TestObject", "Stack trace here" }
                }
            };

            _mockTimeSeriesStorage.Setup(m => m.GetMemoryMetrics(componentId, startTime, endTime))
                                .ReturnsAsync(mockMemoryMetrics);
            
            _mockMemoryLeakDetector.Setup(m => m.AnalyzeMemoryPatterns(componentId))
                                  .ReturnsAsync(mockAnalysis);

            // Act
            var report = await _dashboard.GenerateReport(componentId, startTime, endTime);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(mockMemoryMetrics, report.TimeSeriesData);
            Assert.NotEmpty(report.DetectedLeaks);
            Assert.True(report.FragmentationIndex >= 0 && report.FragmentationIndex <= 100);
            Assert.True(report.GeneratedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task SetAlertThreshold_ShouldUpdateConfiguration()
        {
            // Arrange
            const string metricName = "memory";
            const long threshold = 200 * 1024 * 1024; // 200 MB

            // Act
            var result = await _dashboard.SetAlertThreshold(metricName, threshold);

            // Assert
            Assert.True(result);
            Assert.Equal(threshold, _analyticsConfig.MemoryThresholdBytes);
        }

        [Fact]
        public async Task GetHistoricalData_ShouldReturnMetricsWithinTimeRange()
        {
            // Arrange
            var componentId = "test-component";
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;
            
            var expectedMetrics = new List<MemoryMetrics>
            {
                new MemoryMetrics { Timestamp = startTime.AddMinutes(10) },
                new MemoryMetrics { Timestamp = startTime.AddMinutes(20) },
                new MemoryMetrics { Timestamp = startTime.AddMinutes(30) }
            };

            _mockTimeSeriesStorage.Setup(m => m.GetMemoryMetrics(componentId, startTime, endTime))
                                .ReturnsAsync(expectedMetrics);

            // Act
            var metrics = await _dashboard.GetHistoricalData(componentId, startTime, endTime);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(expectedMetrics.Count, metrics.Count());
            Assert.All(metrics, m => Assert.True(m.Timestamp >= startTime && m.Timestamp <= endTime));
        }

        [Fact]
        public async Task GenerateReport_WithNoLeaks_ShouldReturnEmptyLeaksList()
        {
            // Arrange
            var componentId = "test-component";
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;
            
            var mockAnalysis = new MemoryLeakAnalysis
            {
                ComponentId = componentId,
                LeakDetected = false,
                ConfidenceLevel = 30
            };

            _mockMemoryLeakDetector.Setup(m => m.AnalyzeMemoryPatterns(componentId))
                                  .ReturnsAsync(mockAnalysis);

            _mockTimeSeriesStorage.Setup(m => m.GetMemoryMetrics(componentId, startTime, endTime))
                                .ReturnsAsync(new List<MemoryMetrics>());

            // Act
            var report = await _dashboard.GenerateReport(componentId, startTime, endTime);

            // Assert
            Assert.NotNull(report);
            Assert.Empty(report.DetectedLeaks);
        }
    }
}