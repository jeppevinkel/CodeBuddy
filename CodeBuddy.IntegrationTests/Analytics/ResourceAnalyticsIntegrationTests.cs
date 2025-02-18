using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Moq;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models;

namespace CodeBuddy.IntegrationTests.Analytics
{
    public class ResourceAnalyticsIntegrationTests
    {
        private readonly ITimeSeriesStorage _timeSeriesStorage;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly IResourceAlertManager _resourceAlertManager;
        private readonly ResourceAnalytics _resourceAnalytics;

        public ResourceAnalyticsIntegrationTests()
        {
            // Using real implementations for integration tests
            _timeSeriesStorage = new TimeSeriesStorage();
            _metricsAggregator = new MetricsAggregator();
            _resourceAlertManager = new ResourceAlertManager();
            
            _resourceAnalytics = new ResourceAnalytics(
                _timeSeriesStorage,
                _metricsAggregator,
                _resourceAlertManager
            );
        }

        [Fact]
        public async Task ResourceAllocationDeallocation_ShouldBeTrackedCorrectly()
        {
            // Arrange
            var resourceData = GenerateResourceUsageData("Pipeline1", "CSharpValidator");

            // Act
            await _resourceAnalytics.StoreResourceUsageDataAsync(resourceData);
            var report = await _resourceAnalytics.GenerateReportAsync(TimeSpan.FromMinutes(5));

            // Assert
            Assert.NotNull(report);
            Assert.Equal(resourceData.CpuUsagePercentage, report.AverageMetrics["CpuUsage"], 2);
            Assert.Equal(resourceData.MemoryUsageMB, report.AverageMetrics["MemoryUsage"], 2);
        }

        [Fact]
        public async Task MemoryUsagePatternAnalysis_ShouldIdentifyPatterns()
        {
            // Arrange
            var patterns = GenerateMemoryUsagePatterns();
            foreach (var pattern in patterns)
            {
                await _resourceAnalytics.StoreResourceUsageDataAsync(pattern);
            }

            // Act
            var recommendations = await _resourceAnalytics.GetOptimizationRecommendationsAsync();

            // Assert
            Assert.NotEmpty(recommendations);
            Assert.Contains(recommendations, r => r.ResourceType == "Memory");
            Assert.All(recommendations, r => Assert.NotNull(r.Justification));
        }

        [Fact]
        public async Task ResourceTrendAnalysis_ShouldTrackTrendsOverTime()
        {
            // Arrange
            var timeSeriesData = GenerateTimeSeriesData(TimeSpan.FromDays(30));
            foreach (var data in timeSeriesData)
            {
                await _resourceAnalytics.StoreResourceUsageDataAsync(data);
            }

            // Act
            var trends = await _resourceAnalytics.AnalyzeUsageTrendsAsync();

            // Assert
            Assert.NotNull(trends.CpuTrend);
            Assert.NotNull(trends.MemoryTrend);
            Assert.NotNull(trends.DiskIOTrend);
            Assert.NotNull(trends.PredictedUsage);
        }

        [Fact]
        public async Task AlertingSystem_ShouldTriggerOnThresholdViolation()
        {
            // Arrange
            var highUsageData = new ResourceUsageData
            {
                CpuUsagePercentage = 95.0,
                MemoryUsageMB = 8192,
                DiskIOBytesPerSecond = 100000000,
                PipelineId = "HighLoadPipeline",
                ValidatorType = "StressTest"
            };

            // Act
            await _resourceAnalytics.StoreResourceUsageDataAsync(highUsageData);
            var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync();

            // Assert
            Assert.NotEmpty(bottlenecks);
            Assert.Contains(bottlenecks, b => b.ResourceType == "CPU" && b.Severity > 0.8);
        }

        [Fact]
        public async Task PerformanceImpactMeasurement_ShouldTrackMetrics()
        {
            // Arrange
            var baselineData = GenerateBaselinePerformanceData();
            var loadTestData = GenerateLoadTestData();

            // Act
            foreach (var data in baselineData.Concat(loadTestData))
            {
                await _resourceAnalytics.StoreResourceUsageDataAsync(data);
            }
            var report = await _resourceAnalytics.GenerateReportAsync(TimeSpan.FromHours(1));

            // Assert
            Assert.NotNull(report.PerformanceMetrics);
            Assert.True(report.PerformanceMetrics.ResponseTime > 0);
            Assert.True(report.PerformanceMetrics.ThroughputPerSecond > 0);
        }

        [Fact]
        public async Task CrossComponentDependencies_ShouldBeAnalyzed()
        {
            // Arrange
            var componentData = GenerateMultiComponentData();
            foreach (var data in componentData)
            {
                await _resourceAnalytics.StoreResourceUsageDataAsync(data);
            }

            // Act
            var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync();
            var recommendations = await _resourceAnalytics.GetOptimizationRecommendationsAsync();

            // Assert
            Assert.NotEmpty(bottlenecks);
            Assert.NotEmpty(recommendations);
            Assert.Contains(recommendations, r => r.Impact.CrossComponentEffect != null);
        }

        private ResourceUsageData GenerateResourceUsageData(string pipelineId, string validatorType)
        {
            return new ResourceUsageData
            {
                CpuUsagePercentage = 45.5,
                MemoryUsageMB = 2048,
                DiskIOBytesPerSecond = 50000000,
                Gen0SizeBytes = 1024 * 1024,
                Gen1SizeBytes = 512 * 1024,
                Gen2SizeBytes = 256 * 1024,
                LohSizeBytes = 128 * 1024,
                FinalizationQueueLength = 10,
                FragmentationPercent = 15.5,
                PipelineId = pipelineId,
                ValidatorType = validatorType
            };
        }

        private IEnumerable<ResourceUsageData> GenerateMemoryUsagePatterns()
        {
            var patterns = new List<ResourceUsageData>();
            // Generate increasing memory usage pattern
            for (int i = 1; i <= 10; i++)
            {
                patterns.Add(new ResourceUsageData
                {
                    MemoryUsageMB = 1024 * i,
                    CpuUsagePercentage = 50.0,
                    DiskIOBytesPerSecond = 10000000,
                    PipelineId = "MemoryTest",
                    ValidatorType = "PatternTest"
                });
            }
            return patterns;
        }

        private IEnumerable<ResourceUsageData> GenerateTimeSeriesData(TimeSpan period)
        {
            var data = new List<ResourceUsageData>();
            var intervals = period.TotalHours;
            
            for (int i = 0; i < intervals; i++)
            {
                data.Add(new ResourceUsageData
                {
                    CpuUsagePercentage = 40 + Math.Sin(i * Math.PI / 12) * 20,
                    MemoryUsageMB = 2048 + Math.Cos(i * Math.PI / 12) * 512,
                    DiskIOBytesPerSecond = 20000000 + Math.Sin(i * Math.PI / 6) * 10000000,
                    PipelineId = "TrendTest",
                    ValidatorType = "TimeSeriesTest"
                });
            }
            return data;
        }

        private IEnumerable<ResourceUsageData> GenerateBaselinePerformanceData()
        {
            return Enumerable.Range(0, 10).Select(_ => new ResourceUsageData
            {
                CpuUsagePercentage = 30.0,
                MemoryUsageMB = 1024,
                DiskIOBytesPerSecond = 5000000,
                PipelineId = "BaselineTest",
                ValidatorType = "PerformanceTest"
            });
        }

        private IEnumerable<ResourceUsageData> GenerateLoadTestData()
        {
            return Enumerable.Range(0, 10).Select(_ => new ResourceUsageData
            {
                CpuUsagePercentage = 75.0,
                MemoryUsageMB = 4096,
                DiskIOBytesPerSecond = 50000000,
                PipelineId = "LoadTest",
                ValidatorType = "PerformanceTest"
            });
        }

        private IEnumerable<ResourceUsageData> GenerateMultiComponentData()
        {
            var components = new[] { "Validator", "Cache", "Pipeline", "Storage" };
            var data = new List<ResourceUsageData>();

            foreach (var component in components)
            {
                data.Add(new ResourceUsageData
                {
                    CpuUsagePercentage = 60.0 + Random.Shared.NextDouble() * 20,
                    MemoryUsageMB = 2048 + Random.Shared.Next(-512, 512),
                    DiskIOBytesPerSecond = 30000000 + Random.Shared.Next(-10000000, 10000000),
                    PipelineId = $"{component}Pipeline",
                    ValidatorType = component
                });
            }
            return data;
        }
    }
}