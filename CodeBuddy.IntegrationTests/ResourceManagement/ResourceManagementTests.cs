using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models.ResourceManagement;
using CodeBuddy.Core.Models.Analytics;
using Xunit;
using FluentAssertions;

namespace CodeBuddy.IntegrationTests.ResourceManagement
{
    public class ResourceManagementTests : IDisposable
    {
        private readonly AdaptiveResourceManager _resourceManager;
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly ResourceAnalytics _analytics;
        private readonly ResourceReleaseMonitor _monitor;

        public ResourceManagementTests()
        {
            _resourceManager = new AdaptiveResourceManager();
            _trendAnalyzer = new ResourceTrendAnalyzer();
            _analytics = new ResourceAnalytics();
            _monitor = new ResourceReleaseMonitor();
        }

        [Fact]
        public async Task ResourceAllocation_ShouldAdaptToWorkload()
        {
            // Arrange
            var workload = new AdaptiveResourceModels.WorkloadProfile
            {
                ExpectedConcurrency = 5,
                PeakMemoryUsage = 512 * 1024 * 1024, // 512MB
                TypicalDuration = TimeSpan.FromMinutes(2)
            };

            // Act
            var allocation = await _resourceManager.AllocateResourcesAsync(workload);
            await Task.Delay(1000); // Simulate workload
            var metrics = await _resourceManager.GetCurrentMetricsAsync();

            // Assert
            metrics.AllocatedMemory.Should().BeLessThanOrEqualTo(workload.PeakMemoryUsage);
            metrics.ActiveWorkers.Should().BeLessThanOrEqualTo(workload.ExpectedConcurrency);
        }

        [Fact]
        public async Task ResourceTrends_ShouldIdentifyPatterns()
        {
            // Arrange
            var usageData = new List<ResourceMetricsModel>
            {
                new ResourceMetricsModel { Timestamp = DateTime.UtcNow.AddHours(-2), MemoryUsage = 100 * 1024 * 1024 },
                new ResourceMetricsModel { Timestamp = DateTime.UtcNow.AddHours(-1), MemoryUsage = 200 * 1024 * 1024 },
                new ResourceMetricsModel { Timestamp = DateTime.UtcNow, MemoryUsage = 300 * 1024 * 1024 }
            };

            // Act
            var trend = await _trendAnalyzer.AnalyzeTrendAsync(usageData);

            // Assert
            trend.MemoryGrowthRate.Should().BeGreaterThan(0);
            trend.PredictedPeakUsage.Should().BeGreaterThan(300 * 1024 * 1024);
        }

        [Fact]
        public async Task ResourceAnalytics_ShouldTrackUsagePatterns()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;

            // Act
            var analytics = await _analytics.GenerateResourceAnalyticsAsync(startTime, endTime);

            // Assert
            analytics.Should().NotBeNull();
            analytics.PeakMemoryUsage.Should().BeGreaterThan(0);
            analytics.AverageResourceUtilization.Should().BeGreaterThan(0);
            analytics.ResourceEfficiencyScore.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ResourceReleaseMonitoring_ShouldTrackCleanup()
        {
            // Arrange
            var resources = new List<string> { "Memory", "FileHandles", "NetworkConnections" };

            // Act
            await _monitor.StartMonitoringAsync(resources);
            await Task.Delay(1000); // Simulate resource usage
            var report = await _monitor.GenerateReleaseReportAsync();

            // Assert
            report.Should().NotBeNull();
            report.UnreleasedResources.Should().BeEmpty();
            report.ResourceReleaseLatency.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CrossComponentResourceManagement_ShouldCoordinate()
        {
            // Arrange
            var components = new[] { "ValidationPipeline", "CacheManager", "MetricsCollector" };
            
            // Act
            await _resourceManager.RegisterComponentsAsync(components);
            var allocation = await _resourceManager.GetComponentAllocationsAsync();
            
            // Assert
            allocation.Should().NotBeNull();
            allocation.Keys.Should().Contain(components);
            allocation.Values.Should().AllBeGreaterThan(0);
        }

        public void Dispose()
        {
            _resourceManager.Dispose();
            _monitor.Dispose();
            _analytics.Dispose();
        }
    }
}