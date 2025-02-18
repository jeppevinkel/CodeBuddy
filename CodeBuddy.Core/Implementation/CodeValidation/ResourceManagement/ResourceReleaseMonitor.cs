using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourceReleaseMonitor : IDisposable
    {
        private readonly ConcurrentDictionary<string, ResourceAllocationInfo> _activeResources;
        private readonly ResourcePreallocationManager _preallocationManager;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly ResourceAlertManager _alertManager;
        private readonly Timer _cleanupTimer;
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly ResourceAnalyticsController _analyticsController;
        
        private const int DEFAULT_CLEANUP_INTERVAL = 300000; // 5 minutes
        private const int DEFAULT_RESOURCE_TIMEOUT = 3600000; // 1 hour

        public ResourceReleaseMonitor(
            ResourcePreallocationManager preallocationManager,
            IMetricsAggregator metricsAggregator,
            ResourceAlertManager alertManager,
            ResourceTrendAnalyzer trendAnalyzer,
            ResourceAnalyticsController analyticsController)
        {
            _activeResources = new ConcurrentDictionary<string, ResourceAllocationInfo>();
            _preallocationManager = preallocationManager;
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
            _trendAnalyzer = trendAnalyzer;
            _analyticsController = analyticsController;
            
            _cleanupTimer = new Timer(
                CleanupOrphanedResources, 
                null, 
                DEFAULT_CLEANUP_INTERVAL, 
                DEFAULT_CLEANUP_INTERVAL);
        }

        public void TrackAllocation(string resourceId, ResourceType resourceType, string owner)
        {
            var allocationInfo = new ResourceAllocationInfo
            {
                ResourceId = resourceId,
                Type = resourceType,
                Owner = owner,
                AllocationTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow,
                State = ResourceState.Active
            };

            _activeResources.TryAdd(resourceId, allocationInfo);
            _metricsAggregator.TrackResourceAllocation(allocationInfo);
            _trendAnalyzer.AddDataPoint(resourceType, ResourceMetricType.Allocation);
        }

        public void TrackRelease(string resourceId)
        {
            if (_activeResources.TryRemove(resourceId, out var resourceInfo))
            {
                resourceInfo.ReleaseTime = DateTime.UtcNow;
                resourceInfo.State = ResourceState.Released;
                
                _metricsAggregator.TrackResourceRelease(resourceInfo);
                _trendAnalyzer.AddDataPoint(resourceInfo.Type, ResourceMetricType.Release);
                _preallocationManager.NotifyResourceRelease(resourceInfo);
            }
        }

        public async Task ProcessStuckAllocations()
        {
            var threshold = DateTime.UtcNow.AddMilliseconds(-DEFAULT_RESOURCE_TIMEOUT);
            var stuckResources = new List<ResourceAllocationInfo>();

            foreach (var resource in _activeResources)
            {
                if (resource.Value.LastAccessTime < threshold)
                {
                    stuckResources.Add(resource.Value);
                    await _alertManager.RaiseResourceAlert(
                        ResourceAlertType.StuckAllocation,
                        resource.Value);
                }
            }

            _analyticsController.LogStuckResources(stuckResources);
        }

        private void CleanupOrphanedResources(object state)
        {
            var orphanedResources = new List<ResourceAllocationInfo>();
            var now = DateTime.UtcNow;

            foreach (var resource in _activeResources)
            {
                if (!IsResourceValid(resource.Value))
                {
                    if (_activeResources.TryRemove(resource.Key, out var resourceInfo))
                    {
                        orphanedResources.Add(resourceInfo);
                        _analyticsController.LogOrphanedResource(resourceInfo);
                    }
                }
            }

            if (orphanedResources.Count > 0)
            {
                _alertManager.RaiseOrphanedResourcesAlert(orphanedResources);
                _preallocationManager.HandleOrphanedResources(orphanedResources);
            }
        }

        private bool IsResourceValid(ResourceAllocationInfo resource)
        {
            // Check if the resource owner still exists and the resource is accessible
            return resource.State == ResourceState.Active && 
                   (DateTime.UtcNow - resource.LastAccessTime).TotalMilliseconds < DEFAULT_RESOURCE_TIMEOUT;
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            foreach (var resource in _activeResources)
            {
                TrackRelease(resource.Key);
            }
        }
    }

    public class ResourceAllocationInfo
    {
        public string ResourceId { get; set; }
        public ResourceType Type { get; set; }
        public string Owner { get; set; }
        public DateTime AllocationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime? ReleaseTime { get; set; }
        public ResourceState State { get; set; }
    }

    public enum ResourceState
    {
        Active,
        Released,
        Orphaned
    }

    public enum ResourceType
    {
        Memory,
        FileHandle,
        DatabaseConnection,
        NetworkSocket,
        ThreadPool
    }

    public enum ResourceMetricType
    {
        Allocation,
        Release,
        Timeout,
        Orphaned
    }
}