using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourcePreallocationManager
    {
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly ResourceAnalyticsController _analyticsController;
        private readonly Dictionary<ResourceType, int> _preallocationPool;
        private readonly object _poolLock = new object();

        public ResourcePreallocationManager(
            ResourceTrendAnalyzer trendAnalyzer,
            IMetricsAggregator metricsAggregator,
            ResourceAnalyticsController analyticsController)
        {
            _trendAnalyzer = trendAnalyzer;
            _metricsAggregator = metricsAggregator;
            _analyticsController = analyticsController;
            _preallocationPool = new Dictionary<ResourceType, int>();
        }

        public void NotifyResourceRelease(ResourceAllocationInfo resourceInfo)
        {
            _trendAnalyzer.AddDataPoint(resourceInfo.Type, ResourceMetricType.Release);
            UpdatePreallocationPool(resourceInfo.Type);
        }

        public void HandleOrphanedResources(List<ResourceAllocationInfo> orphanedResources)
        {
            foreach (var resource in orphanedResources)
            {
                _trendAnalyzer.AddDataPoint(resource.Type, ResourceMetricType.Orphaned);
                _metricsAggregator.TrackOrphanedResource(resource);
            }

            var resourceTypeCounts = orphanedResources
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in resourceTypeCounts)
            {
                UpdatePreallocationPool(kvp.Key, kvp.Value);
            }

            _analyticsController.LogOrphanedResourcesBatch(orphanedResources);
        }

        private void UpdatePreallocationPool(ResourceType resourceType, int adjustment = 1)
        {
            lock (_poolLock)
            {
                if (!_preallocationPool.ContainsKey(resourceType))
                {
                    _preallocationPool[resourceType] = 0;
                }

                _preallocationPool[resourceType] = Math.Max(0, _preallocationPool[resourceType] + adjustment);
            }
        }

        public async Task<int> GetPreallocationCount(ResourceType resourceType)
        {
            var trend = await _trendAnalyzer.GetResourceTrend(resourceType);
            
            lock (_poolLock)
            {
                return _preallocationPool.TryGetValue(resourceType, out var count) ? count : 0;
            }
        }

        public Dictionary<ResourceType, int> GetCurrentPoolState()
        {
            lock (_poolLock)
            {
                return new Dictionary<ResourceType, int>(_preallocationPool);
            }
        }
    }
}