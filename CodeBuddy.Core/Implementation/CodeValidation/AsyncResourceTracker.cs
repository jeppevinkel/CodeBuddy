using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Tracks and manages async operations to prevent resource leaks and provide monitoring capabilities.
    /// Integrates with ResourceUsageTracker and PerformanceMonitor for comprehensive resource management.
    /// </summary>
    public class AsyncResourceTracker : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ResourceUsageTracker _resourceTracker;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly AsyncOperationMetrics _metrics;
        private readonly ConcurrentDictionary<Guid, TrackedOperation> _operations;
        private readonly CancellationTokenSource _monitorCts;
        private readonly Task _monitoringTask;
        private readonly SemaphoreSlim _operationThrottle;
        private readonly ConcurrentDictionary<string, ResourceQuota> _quotas;
        private readonly int _maxConcurrentOperations;
        private readonly TimeSpan _operationTimeout;
        private readonly TimeSpan _monitoringInterval;
        private bool _disposed;

        public IReadOnlyCollection<TrackedOperation> ActiveOperations => _operations.Values.ToList().AsReadOnly();
        public int CurrentOperationCount => _operations.Count;
        public IReadOnlyDictionary<string, DashboardMetrics> Metrics => _metrics.GetDashboardMetrics();

        public AsyncResourceTracker(
            ILogger logger,
            ResourceUsageTracker resourceTracker,
            PerformanceMonitor performanceMonitor,
            int maxConcurrentOperations = 100,
            TimeSpan? operationTimeout = null,
            TimeSpan? monitoringInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resourceTracker = resourceTracker ?? throw new ArgumentNullException(nameof(resourceTracker));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _metrics = new AsyncOperationMetrics(logger);
            _operations = new ConcurrentDictionary<Guid, TrackedOperation>();
            _quotas = new ConcurrentDictionary<string, ResourceQuota>();
            _monitorCts = new CancellationTokenSource();
            _maxConcurrentOperations = maxConcurrentOperations;
            _operationThrottle = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
            _operationTimeout = operationTimeout ?? TimeSpan.FromMinutes(5);
            _monitoringInterval = monitoringInterval ?? TimeSpan.FromSeconds(30);

            // Initialize default quotas
            _quotas["memory"] = new ResourceQuota { Type = "memory", Limit = 1024 * 1024 * 1024 }; // 1GB
            _quotas["operations"] = new ResourceQuota { Type = "operations", Limit = maxConcurrentOperations };
            _quotas["duration"] = new ResourceQuota { Type = "duration", Limit = _operationTimeout.TotalMilliseconds };

            // Start monitoring task
            _monitoringTask = MonitorOperationsAsync(_monitorCts.Token);
        }

        /// <summary>
        /// Tracks an async operation and ensures it completes within the timeout period.
        /// Integrates with resource tracking and monitoring systems.
        /// </summary>
        public async Task<T> TrackOperationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            string parentOperationId = null,
            IDictionary<string, string> tags = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AsyncResourceTracker));

            await _operationThrottle.WaitAsync(cancellationToken);

            var operationId = Guid.NewGuid();
            var trackedOp = new TrackedOperation(operationId, operationName)
            {
                ParentOperationId = parentOperationId
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    trackedOp.Tags[tag.Key] = tag.Value;
                }
            }

            _operations[operationId] = trackedOp;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_operationTimeout);

                var startMemory = GC.GetTotalMemory(false);
                var startThreads = Process.GetCurrentProcess().Threads.Count;
                var startHandles = Process.GetCurrentProcess().HandleCount;
                var sw = Stopwatch.StartNew();

                trackedOp.Start();
                _performanceMonitor.StartOperation(operationName);

                var result = await operation(timeoutCts.Token);

                sw.Stop();
                var endMemory = GC.GetTotalMemory(false);
                var endThreads = Process.GetCurrentProcess().Threads.Count;
                var endHandles = Process.GetCurrentProcess().HandleCount;

                var resourceUsage = new ResourceUsage
                {
                    OperationType = operationName,
                    MemoryUsed = endMemory - startMemory,
                    Duration = sw.Elapsed,
                    ThreadCount = endThreads - startThreads,
                    HandleCount = endHandles - startHandles,
                    Tags = trackedOp.Tags
                };

                trackedOp.Complete(resourceUsage);
                _performanceMonitor.StopOperation(operationName);

                // Record metrics
                _metrics.RecordOperation(operationName, sw.Elapsed, resourceUsage.MemoryUsed, true);

                // Update resource tracking
                await _resourceTracker.TrackResourceUsageAsync(resourceUsage);

                // Check quotas
                CheckQuotas(resourceUsage);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                trackedOp.Cancel();
                _metrics.RecordOperation(operationName, trackedOp.Duration ?? TimeSpan.Zero, 0, false);
                throw;
            }
            catch (Exception ex)
            {
                trackedOp.Fail(ex);
                _metrics.RecordOperation(operationName, trackedOp.Duration ?? TimeSpan.Zero, 0, false);
                _logger.LogError(ex, "Operation {OperationName} failed", operationName);
                throw;
            }
            finally
            {
                _operations.TryRemove(operationId, out _);
                _operationThrottle.Release();
            }
        }

        private async Task MonitorOperationsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_monitoringInterval, cancellationToken);
                    await CheckForStaleOperationsAsync();
                    await MonitorResourceUsageAsync();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring async operations");
                }
            }
        }

        private async Task MonitorResourceUsageAsync()
        {
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;
            var threadCount = process.Threads.Count;
            var handleCount = process.HandleCount;

            var metrics = new Dictionary<string, long>
            {
                ["ActiveOperations"] = _operations.Count,
                ["MemoryUsage"] = memoryUsage,
                ["ThreadCount"] = threadCount,
                ["HandleCount"] = handleCount
            };

            foreach (var metric in metrics)
            {
                _logger.LogInformation("{Metric}: {Value}", metric.Key, metric.Value);
            }

            // Check for resource pressure
            if (memoryUsage > _quotas["memory"].Limit)
            {
                _logger.LogWarning("Memory usage exceeds quota: {Usage} > {Limit}",
                    memoryUsage, _quotas["memory"].Limit);
                await EmergencyCleanupAsync();
            }
        }

        private async Task CheckForStaleOperationsAsync()
        {
            var now = DateTime.UtcNow;
            var staleOperations = _operations.Values
                .Where(op => now - op.StartTime > _operationTimeout)
                .ToList();

            foreach (var operation in staleOperations)
            {
                _logger.LogWarning(
                    "Stale operation detected - ID: {OperationId}, Name: {OperationName}, Duration: {Duration}, Tags: {@Tags}",
                    operation.Id,
                    operation.Name,
                    now - operation.StartTime,
                    operation.Tags);

                if (_operations.TryRemove(operation.Id, out _))
                {
                    operation.Cancel();
                    _operationThrottle.Release();

                    // Record stale operation metrics
                    _metrics.RecordOperation(
                        operation.Name,
                        now - operation.StartTime,
                        operation.ResourceUsage?.MemoryUsed ?? 0,
                        false);
                }
            }
        }

        private void CheckQuotas(ResourceUsage usage)
        {
            if (usage.MemoryUsed > _quotas["memory"].Limit)
            {
                _logger.LogWarning("Operation {Operation} exceeded memory quota: {Used} > {Limit}",
                    usage.OperationType, usage.MemoryUsed, _quotas["memory"].Limit);
            }

            if (usage.Duration.TotalMilliseconds > _quotas["duration"].Limit)
            {
                _logger.LogWarning("Operation {Operation} exceeded duration quota: {Used}ms > {Limit}ms",
                    usage.OperationType, usage.Duration.TotalMilliseconds, _quotas["duration"].Limit);
            }
        }

        private async Task EmergencyCleanupAsync()
        {
            _logger.LogWarning("Initiating emergency cleanup");

            // Cancel long-running operations
            var longRunningOps = _operations.Values
                .Where(op => op.Duration > TimeSpan.FromMinutes(1))
                .ToList();

            foreach (var op in longRunningOps)
            {
                if (_operations.TryRemove(op.Id, out _))
                {
                    op.Cancel();
                    _operationThrottle.Release();
                }
            }

            // Force garbage collection
            GC.Collect(2, GCCollectionMode.Aggressive, true);
            await Task.Delay(100); // Allow GC to complete
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _monitorCts.Cancel();
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal
            }

            // Cancel all tracked operations
            foreach (var operation in _operations.Values)
            {
                operation.Cancel();
            }
            _operations.Clear();

            _operationThrottle.Dispose();
            _monitorCts.Dispose();
        }

        public class TrackedOperation
        {
            public Guid Id { get; }
            public string Name { get; }
            public string ParentOperationId { get; set; }
            public DateTime StartTime { get; private set; }
            public DateTime? CompletionTime { get; private set; }
            public TimeSpan? Duration => Status != OperationStatus.Running ?
                (CompletionTime ?? DateTime.UtcNow) - StartTime : null;
            public OperationStatus Status { get; private set; }
            public Exception Error { get; private set; }
            public ResourceUsage ResourceUsage { get; private set; }
            public IDictionary<string, string> Tags { get; } = new Dictionary<string, string>();

            public TrackedOperation(Guid id, string name)
            {
                Id = id;
                Name = name;
                Status = OperationStatus.Created;
            }

            public void Start()
            {
                StartTime = DateTime.UtcNow;
                Status = OperationStatus.Running;
            }

            public void Complete(ResourceUsage resourceUsage)
            {
                Status = OperationStatus.Completed;
                CompletionTime = DateTime.UtcNow;
                ResourceUsage = resourceUsage;
            }

            public void Cancel()
            {
                Status = OperationStatus.Cancelled;
                CompletionTime = DateTime.UtcNow;
            }

            public void Fail(Exception error)
            {
                Error = error;
                Status = OperationStatus.Failed;
                CompletionTime = DateTime.UtcNow;
            }
        }

        public enum OperationStatus
        {
            Created,
            Running,
            Completed,
            Failed,
            Cancelled
        }

        private class ResourceQuota
        {
            public string Type { get; set; }
            public long Limit { get; set; }
        }
    }
}