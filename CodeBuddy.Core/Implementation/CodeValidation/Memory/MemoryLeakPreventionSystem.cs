using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Logging;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models.ResourceManagement;

namespace CodeBuddy.Core.Implementation.CodeValidation.Memory
{
    public class MemoryLeakPreventionSystem
    {
        private readonly IResourceLoggingService _loggingService;
        private readonly ResourceMonitoringDashboard _dashboard;
        private readonly Dictionary<string, WeakReference> _resourceTracker;
        private readonly object _trackerLock = new object();

        public MemoryLeakPreventionSystem(
            IResourceLoggingService loggingService,
            ResourceMonitoringDashboard dashboard)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _resourceTracker = new Dictionary<string, WeakReference>();
        }

        public async Task<LeakCheckResult> CheckForLeaks()
        {
            var result = new LeakCheckResult();
            var leakedResources = new List<string>();
            long reclaimedMemory = 0;

            lock (_trackerLock)
            {
                foreach (var kvp in _resourceTracker)
                {
                    if (!kvp.Value.IsAlive)
                    {
                        leakedResources.Add(kvp.Key);
                        result.LeaksDetected++;
                    }
                }

                // Remove leaked resources from tracker
                foreach (var resourceId in leakedResources)
                {
                    _resourceTracker.Remove(resourceId);
                    reclaimedMemory += 1024; // Estimated memory per resource
                }
            }

            result.ReclaimedBytes = reclaimedMemory;

            var metrics = new Dictionary<string, object>
            {
                { "LeaksDetected", result.LeaksDetected },
                { "ReclaimedMemory", result.ReclaimedBytes },
                { "CheckTime", DateTime.UtcNow },
                { "LeakedResourceIds", string.Join(",", leakedResources) }
            };

            if (result.LeaksDetected > 0)
            {
                _loggingService.LogResourceWarning(
                    "MemoryLeak",
                    $"Detected {result.LeaksDetected} potential memory leaks",
                    metrics,
                    nameof(MemoryLeakPreventionSystem));
            }
            else
            {
                _loggingService.LogResourceAllocation(
                    "MemoryLeak",
                    "LeakCheck",
                    metrics,
                    nameof(MemoryLeakPreventionSystem));
            }

            await _dashboard.UpdateLeakDetectionMetrics(metrics);
            return result;
        }

        public void TrackResource(ResourceInfo resource)
        {
            lock (_trackerLock)
            {
                _resourceTracker[resource.Id] = new WeakReference(resource);
            }

            _loggingService.LogResourceAllocation(
                resource.ResourceType.ToString(),
                "ResourceTracking",
                new Dictionary<string, object>
                {
                    { "ResourceId", resource.Id },
                    { "ResourceType", resource.ResourceType },
                    { "TrackingStartTime", DateTime.UtcNow },
                    { "InitialMemoryUsage", resource.MemoryUsage }
                },
                nameof(MemoryLeakPreventionSystem));
        }

        public async Task PerformEmergencyCleanup()
        {
            var startTime = DateTime.UtcNow;
            var cleanedResources = 0;
            var reclaimedMemory = 0L;

            lock (_trackerLock)
            {
                var deadReferences = new List<string>();
                foreach (var kvp in _resourceTracker)
                {
                    if (!kvp.Value.IsAlive)
                    {
                        deadReferences.Add(kvp.Key);
                        cleanedResources++;
                        reclaimedMemory += 1024; // Estimated memory per resource
                    }
                }

                foreach (var resourceId in deadReferences)
                {
                    _resourceTracker.Remove(resourceId);
                }
            }

            var metrics = new Dictionary<string, object>
            {
                { "CleanedResources", cleanedResources },
                { "ReclaimedMemory", reclaimedMemory },
                { "Duration", DateTime.UtcNow - startTime },
                { "TotalTrackedResources", _resourceTracker.Count }
            };

            _loggingService.LogResourceAllocation(
                "EmergencyCleanup",
                "Cleanup",
                metrics,
                nameof(MemoryLeakPreventionSystem));

            await _dashboard.UpdateEmergencyCleanupMetrics(metrics);
        }
    }

    public class LeakCheckResult
    {
        public int LeaksDetected { get; set; }
        public long ReclaimedBytes { get; set; }
    }
}