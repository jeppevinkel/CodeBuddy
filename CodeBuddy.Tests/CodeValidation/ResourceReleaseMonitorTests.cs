using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ResourceReleaseMonitorTests
    {
        private readonly Mock<ResourcePreallocationManager> _mockPreallocationManager;
        private readonly Mock<IMetricsAggregator> _mockMetricsAggregator;
        private readonly Mock<ResourceAlertManager> _mockAlertManager;
        private readonly Mock<ResourceTrendAnalyzer> _mockTrendAnalyzer;
        private readonly Mock<ResourceAnalyticsController> _mockAnalyticsController;
        private readonly ResourceReleaseMonitor _monitor;

        public ResourceReleaseMonitorTests()
        {
            _mockPreallocationManager = new Mock<ResourcePreallocationManager>();
            _mockMetricsAggregator = new Mock<IMetricsAggregator>();
            _mockAlertManager = new Mock<ResourceAlertManager>();
            _mockTrendAnalyzer = new Mock<ResourceTrendAnalyzer>();
            _mockAnalyticsController = new Mock<ResourceAnalyticsController>();

            _monitor = new ResourceReleaseMonitor(
                _mockPreallocationManager.Object,
                _mockMetricsAggregator.Object,
                _mockAlertManager.Object,
                _mockTrendAnalyzer.Object,
                _mockAnalyticsController.Object);
        }

        [Fact]
        public void TrackAllocation_ShouldTrackNewResource()
        {
            // Arrange
            var resourceId = "test-resource-1";
            var resourceType = ResourceType.Memory;
            var owner = "test-owner";

            // Act
            _monitor.TrackAllocation(resourceId, resourceType, owner);

            // Assert
            _mockMetricsAggregator.Verify(m => m.TrackResourceAllocation(
                It.Is<ResourceAllocationInfo>(r => 
                    r.ResourceId == resourceId && 
                    r.Type == resourceType && 
                    r.Owner == owner)));
            
            _mockTrendAnalyzer.Verify(t => t.AddDataPoint(
                resourceType, ResourceMetricType.Allocation));
        }

        [Fact]
        public void TrackRelease_ShouldReleaseAndNotifyPreallocationManager()
        {
            // Arrange
            var resourceId = "test-resource-1";
            var resourceType = ResourceType.Memory;
            var owner = "test-owner";
            
            _monitor.TrackAllocation(resourceId, resourceType, owner);

            // Act
            _monitor.TrackRelease(resourceId);

            // Assert
            _mockMetricsAggregator.Verify(m => m.TrackResourceRelease(
                It.Is<ResourceAllocationInfo>(r => 
                    r.ResourceId == resourceId && 
                    r.State == ResourceState.Released)));
            
            _mockPreallocationManager.Verify(p => p.NotifyResourceRelease(
                It.Is<ResourceAllocationInfo>(r => r.ResourceId == resourceId)));
        }

        [Fact]
        public async Task ProcessStuckAllocations_ShouldIdentifyAndAlertStuckResources()
        {
            // Arrange
            var resourceId = "test-resource-1";
            var resourceType = ResourceType.Memory;
            var owner = "test-owner";
            
            _monitor.TrackAllocation(resourceId, resourceType, owner);

            // Act
            await Task.Delay(1000); // Simulate time passage
            await _monitor.ProcessStuckAllocations();

            // Assert
            _mockAlertManager.Verify(a => a.RaiseResourceAlert(
                ResourceAlertType.StuckAllocation,
                It.Is<ResourceAllocationInfo>(r => r.ResourceId == resourceId)));
        }

        [Fact]
        public void Dispose_ShouldReleaseAllActiveResources()
        {
            // Arrange
            var resources = new[]
            {
                ("resource-1", ResourceType.Memory, "owner-1"),
                ("resource-2", ResourceType.FileHandle, "owner-2")
            };

            foreach (var (id, type, owner) in resources)
            {
                _monitor.TrackAllocation(id, type, owner);
            }

            // Act
            _monitor.Dispose();

            // Assert
            foreach (var (id, _, _) in resources)
            {
                _mockMetricsAggregator.Verify(m => m.TrackResourceRelease(
                    It.Is<ResourceAllocationInfo>(r => 
                        r.ResourceId == id && 
                        r.State == ResourceState.Released)));
            }
        }
    }
}