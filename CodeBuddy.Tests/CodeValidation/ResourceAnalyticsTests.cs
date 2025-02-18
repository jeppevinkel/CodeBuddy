using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ResourceAnalyticsTests
    {
        private readonly Mock<ITimeSeriesStorage> _mockTimeSeriesStorage;
        private readonly Mock<IMetricsAggregator> _mockMetricsAggregator;
        private readonly Mock<IResourceAlertManager> _mockResourceAlertManager;
        private readonly ResourceAnalytics _resourceAnalytics;

        public ResourceAnalyticsTests()
        {
            _mockTimeSeriesStorage = new Mock<ITimeSeriesStorage>();
            _mockMetricsAggregator = new Mock<IMetricsAggregator>();
            _mockResourceAlertManager = new Mock<IResourceAlertManager>();

            _resourceAnalytics = new ResourceAnalytics(
                _mockTimeSeriesStorage.Object,
                _mockMetricsAggregator.Object,
                _mockResourceAlertManager.Object);
        }

        [Fact]
        public async Task StoreResourceUsageData_StoresDataInTimeSeriesStorage()
        {
            // Arrange
            var data = new ResourceUsageData
            {
                PipelineId = "test-pipeline",
                ValidatorType = "test-validator",
                CpuUsagePercentage = 75.5,
                MemoryUsageMB = 1024.0,
                DiskIOBytesPerSecond = 5242880,
                Timestamp = DateTime.UtcNow
            };

            // Act
            await _resourceAnalytics.StoreResourceUsageDataAsync(data);

            // Assert
            _mockTimeSeriesStorage.Verify(
                x => x.StoreDataPointAsync(It.Is<TimeSeriesDataPoint>(p =>
                    p.Metrics["CpuUsage"] == data.CpuUsagePercentage &&
                    p.Metrics["MemoryUsage"] == data.MemoryUsageMB &&
                    p.Metrics["DiskIORate"] == data.DiskIOBytesPerSecond &&
                    p.Tags["PipelineId"] == data.PipelineId &&
                    p.Tags["ValidatorType"] == data.ValidatorType)),
                Times.Once);
        }

        [Fact]
        public async Task GenerateReport_ReturnsCompleteResourceUsageReport()
        {
            // Arrange
            var period = TimeSpan.FromHours(1);
            var endTime = DateTime.UtcNow;
            var startTime = endTime - period;

            var timeSeriesData = new List<TimeSeriesDataPoint>
            {
                new TimeSeriesDataPoint
                {
                    Timestamp = startTime.AddMinutes(15),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 65.5 },
                        { "MemoryUsage", 1024.0 },
                        { "DiskIORate", 5242880 }
                    }
                },
                new TimeSeriesDataPoint
                {
                    Timestamp = startTime.AddMinutes(30),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 75.5 },
                        { "MemoryUsage", 1124.0 },
                        { "DiskIORate", 6242880 }
                    }
                }
            };

            _mockTimeSeriesStorage.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(timeSeriesData);

            // Act
            var report = await _resourceAnalytics.GenerateReportAsync(period);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(period, report.Period);
            Assert.True((endTime - report.EndTime).TotalSeconds < 1);
            Assert.True((startTime - report.StartTime).TotalSeconds < 1);
            Assert.NotNull(report.ResourceUtilization);
            Assert.NotNull(report.PerformanceMetrics);
        }

        [Fact]
        public async Task GetOptimizationRecommendations_ReturnsValidRecommendations()
        {
            // Arrange
            var timeSeriesData = new List<TimeSeriesDataPoint>
            {
                new TimeSeriesDataPoint
                {
                    Timestamp = DateTime.UtcNow.AddHours(-6),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 85.5 },
                        { "MemoryUsage", 2048.0 },
                        { "DiskIORate", 10485760 }
                    }
                }
            };

            _mockTimeSeriesStorage.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(timeSeriesData);

            // Act
            var recommendations = (await _resourceAnalytics.GetOptimizationRecommendationsAsync()).ToList();

            // Assert
            Assert.NotEmpty(recommendations);
            Assert.Contains(recommendations, r => r.ResourceType == "CPU");
            Assert.Contains(recommendations, r => r.ResourceType == "Memory");
            Assert.All(recommendations, r =>
            {
                Assert.NotNull(r.Justification);
                Assert.NotNull(r.Impact);
                Assert.True(r.CurrentUsage > 0);
                Assert.True(r.RecommendedUsage > 0);
                Assert.True(r.RecommendedUsage <= r.CurrentUsage);
            });
        }

        [Fact]
        public async Task AnalyzeUsageTrends_ReturnsValidTrends()
        {
            // Arrange
            var timeSeriesData = new List<TimeSeriesDataPoint>();
            var startTime = DateTime.UtcNow.AddMonths(-1);
            
            for (int i = 0; i < 30; i++)
            {
                timeSeriesData.Add(new TimeSeriesDataPoint
                {
                    Timestamp = startTime.AddDays(i),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 50.0 + i },
                        { "MemoryUsage", 1024.0 + (i * 50) },
                        { "DiskIORate", 5242880 + (i * 100000) }
                    }
                });
            }

            _mockTimeSeriesStorage.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(timeSeriesData);

            // Act
            var trends = await _resourceAnalytics.AnalyzeUsageTrendsAsync();

            // Assert
            Assert.NotNull(trends);
            Assert.NotNull(trends.CpuTrend);
            Assert.NotNull(trends.MemoryTrend);
            Assert.NotNull(trends.DiskIOTrend);
            
            Assert.True(trends.CpuTrend.Slope > 0);
            Assert.True(trends.MemoryTrend.Slope > 0);
            Assert.True(trends.DiskIOTrend.Slope > 0);
            
            Assert.True(trends.CpuTrend.Correlation > 0.9);
            Assert.True(trends.MemoryTrend.Correlation > 0.9);
            Assert.True(trends.DiskIOTrend.Correlation > 0.9);
        }

        [Fact]
        public async Task IdentifyBottlenecks_ReturnsValidBottlenecks()
        {
            // Arrange
            var timeSeriesData = new List<TimeSeriesDataPoint>
            {
                new TimeSeriesDataPoint
                {
                    Timestamp = DateTime.UtcNow.AddHours(-23),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 95.5 },
                        { "MemoryUsage", 3072.0 },
                        { "DiskIORate", 15728640 }
                    }
                },
                new TimeSeriesDataPoint
                {
                    Timestamp = DateTime.UtcNow.AddHours(-22),
                    Metrics = new Dictionary<string, double>
                    {
                        { "CpuUsage", 97.5 },
                        { "MemoryUsage", 3584.0 },
                        { "DiskIORate", 20971520 }
                    }
                }
            };

            _mockTimeSeriesStorage.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(timeSeriesData);

            // Act
            var bottlenecks = (await _resourceAnalytics.IdentifyBottlenecksAsync()).ToList();

            // Assert
            Assert.NotEmpty(bottlenecks);
            Assert.Contains(bottlenecks, b => b.ResourceType == "CPU");
            Assert.Contains(bottlenecks, b => b.ResourceType == "Memory");
            Assert.Contains(bottlenecks, b => b.ResourceType == "DiskIO");
            
            Assert.All(bottlenecks, b =>
            {
                Assert.True(b.Severity > 0);
                Assert.NotNull(b.Impact);
                Assert.NotNull(b.RecommendedAction);
                Assert.NotEmpty(b.AffectedOperations);
            });
        }
    }
}