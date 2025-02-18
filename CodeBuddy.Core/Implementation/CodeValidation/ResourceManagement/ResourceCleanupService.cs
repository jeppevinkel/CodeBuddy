using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.ResourceManagement;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourceCleanupService
    {
        private readonly MemoryLeakPreventionSystem _memoryLeakPrevention;
        private readonly IResourceLoggingService _loggingService;
        private readonly ResourceReleaseMonitor _releaseMonitor;

        public ResourceCleanupService(
            MemoryLeakPreventionSystem memoryLeakPrevention,
            ResourceReleaseMonitor releaseMonitor,
            IResourceLoggingService loggingService)
        {
            _memoryLeakPrevention = memoryLeakPrevention ?? throw new ArgumentNullException(nameof(memoryLeakPrevention));
            _releaseMonitor = releaseMonitor ?? throw new ArgumentNullException(nameof(releaseMonitor));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public async Task CleanupResources(IEnumerable<ResourceInfo> resources)
        {
            var startTime = DateTime.UtcNow;
            var metrics = new Dictionary<string, object>
            {
                { "ResourceCount", resources.Count() },
                { "CleanupStartTime", startTime }
            };

            _loggingService.LogResourceAllocation(
                "ResourceCleanup",
                "CleanupStart",
                metrics,
                nameof(ResourceCleanupService));

            var successCount = 0;
            var failureCount = 0;

            foreach (var resource in resources)
            {
                try
                {
                    await ReleaseResource(resource);
                    successCount++;
                    
                    _loggingService.LogResourceDeallocation(
                        resource.ResourceType.ToString(),
                        "ResourceRelease",
                        new Dictionary<string, object>
                        {
                            { "ResourceId", resource.Id },
                            { "ReleaseTime", DateTime.UtcNow },
                            { "ResourceAge", DateTime.UtcNow - resource.CreationTime },
                            { "ResourceType", resource.ResourceType },
                            { "ReleaseSuccess", true }
                        },
                        nameof(ResourceCleanupService));
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _loggingService.LogResourceError(
                        resource.ResourceType.ToString(),
                        $"Failed to release resource {resource.Id}",
                        ex,
                        nameof(ResourceCleanupService));
                }
            }

            var leakCheckResult = await _memoryLeakPrevention.CheckForLeaks();
            var completionTime = DateTime.UtcNow;
            
            _loggingService.LogResourceAllocation(
                "ResourceCleanup",
                "CleanupComplete",
                new Dictionary<string, object>
                {
                    { "ResourceCount", resources.Count() },
                    { "SuccessCount", successCount },
                    { "FailureCount", failureCount },
                    { "CompletionTime", completionTime },
                    { "TotalDuration", completionTime - startTime },
                    { "LeaksDetected", leakCheckResult.LeaksDetected },
                    { "MemoryReclaimed", leakCheckResult.ReclaimedBytes }
                },
                nameof(ResourceCleanupService));

            await _releaseMonitor.UpdateReleaseMetrics(new ReleaseMetrics
            {
                TotalResources = resources.Count(),
                SuccessfulReleases = successCount,
                FailedReleases = failureCount,
                LeaksDetected = leakCheckResult.LeaksDetected,
                MemoryReclaimed = leakCheckResult.ReclaimedBytes,
                ExecutionTime = completionTime - startTime
            });
        }

        private async Task ReleaseResource(ResourceInfo resource)
        {
            var releaseStart = DateTime.UtcNow;
            try
            {
                await _releaseMonitor.BeginResourceRelease(resource);
                await resource.Release();
                await _releaseMonitor.CompleteResourceRelease(resource);

                _loggingService.LogResourceAllocation(
                    resource.ResourceType.ToString(),
                    "ResourceReleaseDetails",
                    new Dictionary<string, object>
                    {
                        { "ResourceId", resource.Id },
                        { "ReleaseTime", DateTime.UtcNow },
                        { "ReleaseDuration", DateTime.UtcNow - releaseStart },
                        { "ResourceType", resource.ResourceType },
                        { "ReleaseSuccess", true },
                        { "MemoryFreed", resource.MemoryUsage }
                    },
                    nameof(ResourceCleanupService));
            }
            catch (Exception ex)
            {
                await _releaseMonitor.FailResourceRelease(resource, ex);
                throw;
            }
        }
    }
}