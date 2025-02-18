using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.ResourceManagement;
using CodeBuddy.Core.Implementation.CodeValidation.Logging;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class ResourceReleaseMonitor
    {
        private readonly IResourceLoggingService _loggingService;
        private readonly ResourceMonitoringDashboard _dashboard;
        private readonly ConcurrentDictionary<string, ResourceReleaseOperation> _activeOperations;

        public ResourceReleaseMonitor(
            IResourceLoggingService loggingService,
            ResourceMonitoringDashboard dashboard)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _activeOperations = new ConcurrentDictionary<string, ResourceReleaseOperation>();
        }

        public async Task BeginResourceRelease(ResourceInfo resource)
        {
            var operation = new ResourceReleaseOperation
            {
                ResourceId = resource.Id,
                ResourceType = resource.ResourceType,
                StartTime = DateTime.UtcNow,
                Status = ReleaseStatus.InProgress
            };

            _activeOperations.TryAdd(resource.Id, operation);

            var metrics = new Dictionary<string, object>
            {
                { "ResourceId", resource.Id },
                { "ResourceType", resource.ResourceType },
                { "StartTime", operation.StartTime },
                { "InitialMemoryUsage", resource.MemoryUsage }
            };

            _loggingService.LogResourceAllocation(
                resource.ResourceType.ToString(),
                "ReleaseStart",
                metrics,
                nameof(ResourceReleaseMonitor));

            await _dashboard.UpdateResourceOperation(resource.Id, "RELEASE_STARTED", metrics);
        }

        public async Task CompleteResourceRelease(ResourceInfo resource)
        {
            if (_activeOperations.TryGetValue(resource.Id, out var operation))
            {
                operation.EndTime = DateTime.UtcNow;
                operation.Status = ReleaseStatus.Completed;
                operation.Duration = operation.EndTime - operation.StartTime;

                var metrics = new Dictionary<string, object>
                {
                    { "ResourceId", resource.Id },
                    { "ResourceType", resource.ResourceType },
                    { "CompletionTime", operation.EndTime },
                    { "Duration", operation.Duration },
                    { "Status", "Completed" },
                    { "MemoryFreed", resource.MemoryUsage }
                };

                _loggingService.LogResourceDeallocation(
                    resource.ResourceType.ToString(),
                    "ReleaseComplete",
                    metrics,
                    nameof(ResourceReleaseMonitor));

                await _dashboard.UpdateResourceOperation(resource.Id, "RELEASE_COMPLETED", metrics);
                _activeOperations.TryRemove(resource.Id, out _);
            }
        }

        public async Task FailResourceRelease(ResourceInfo resource, Exception error)
        {
            if (_activeOperations.TryGetValue(resource.Id, out var operation))
            {
                operation.EndTime = DateTime.UtcNow;
                operation.Status = ReleaseStatus.Failed;
                operation.Error = error;
                operation.Duration = operation.EndTime - operation.StartTime;

                var metrics = new Dictionary<string, object>
                {
                    { "ResourceId", resource.Id },
                    { "ResourceType", resource.ResourceType },
                    { "FailureTime", operation.EndTime },
                    { "Duration", operation.Duration },
                    { "Status", "Failed" },
                    { "ErrorType", error.GetType().Name },
                    { "ErrorMessage", error.Message }
                };

                _loggingService.LogResourceError(
                    resource.ResourceType.ToString(),
                    $"Resource release failed: {error.Message}",
                    error,
                    nameof(ResourceReleaseMonitor));

                await _dashboard.UpdateResourceOperation(resource.Id, "RELEASE_FAILED", metrics);
                _activeOperations.TryRemove(resource.Id, out _);
            }
        }

        public async Task UpdateReleaseMetrics(ReleaseMetrics metrics)
        {
            var logMetrics = new Dictionary<string, object>
            {
                { "TotalResources", metrics.TotalResources },
                { "SuccessfulReleases", metrics.SuccessfulReleases },
                { "FailedReleases", metrics.FailedReleases },
                { "LeaksDetected", metrics.LeaksDetected },
                { "MemoryReclaimed", metrics.MemoryReclaimed },
                { "ExecutionTime", metrics.ExecutionTime }
            };

            _loggingService.LogResourceAllocation(
                "ReleaseMetrics",
                "MetricsUpdate",
                logMetrics,
                nameof(ResourceReleaseMonitor));

            await _dashboard.UpdateMetrics(logMetrics);
        }
    }

    public class ResourceReleaseOperation
    {
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public ReleaseStatus Status { get; set; }
        public Exception Error { get; set; }
    }

    public enum ReleaseStatus
    {
        InProgress,
        Completed,
        Failed
    }

    public class ReleaseMetrics
    {
        public int TotalResources { get; set; }
        public int SuccessfulReleases { get; set; }
        public int FailedReleases { get; set; }
        public int LeaksDetected { get; set; }
        public long MemoryReclaimed { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}