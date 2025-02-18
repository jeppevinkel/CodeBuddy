using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Tracks and manages async operations to prevent resource leaks.
    /// </summary>
    public class AsyncResourceTracker : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, TrackedOperation> _operations;
        private readonly CancellationTokenSource _monitorCts;
        private readonly Task _monitoringTask;
        private readonly SemaphoreSlim _operationThrottle;
        private readonly int _maxConcurrentOperations;
        private readonly TimeSpan _operationTimeout;
        private readonly TimeSpan _monitoringInterval;
        private bool _disposed;

        public IReadOnlyCollection<TrackedOperation> ActiveOperations => _operations.Values.ToList().AsReadOnly();
        public int CurrentOperationCount => _operations.Count;

        public AsyncResourceTracker(
            ILogger logger,
            int maxConcurrentOperations = 100,
            TimeSpan? operationTimeout = null,
            TimeSpan? monitoringInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operations = new ConcurrentDictionary<Guid, TrackedOperation>();
            _monitorCts = new CancellationTokenSource();
            _maxConcurrentOperations = maxConcurrentOperations;
            _operationThrottle = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
            _operationTimeout = operationTimeout ?? TimeSpan.FromMinutes(5);
            _monitoringInterval = monitoringInterval ?? TimeSpan.FromSeconds(30);

            // Start monitoring task
            _monitoringTask = MonitorOperationsAsync(_monitorCts.Token);
        }

        /// <summary>
        /// Tracks an async operation and ensures it completes within the timeout period.
        /// </summary>
        public async Task<T> TrackOperationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            await _operationThrottle.WaitAsync(cancellationToken);

            var operationId = Guid.NewGuid();
            var trackedOp = new TrackedOperation(operationId, operationName);
            _operations[operationId] = trackedOp;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_operationTimeout);

                trackedOp.Start();
                var result = await operation(timeoutCts.Token);
                trackedOp.Complete();

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                trackedOp.Cancel();
                throw;
            }
            catch (Exception ex)
            {
                trackedOp.Fail(ex);
                throw;
            }
            finally
            {
                _operations.TryRemove(operationId, out _);
                _operationThrottle.Release();
            }
        }

        /// <summary>
        /// Tracks an async operation that doesn't return a value.
        /// </summary>
        public Task TrackOperationAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            return TrackOperationAsync(async ct =>
            {
                await operation(ct);
                return true;
            }, operationName, cancellationToken);
        }

        private async Task MonitorOperationsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_monitoringInterval, cancellationToken);
                    await CheckForStaleOperationsAsync();
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

        private async Task CheckForStaleOperationsAsync()
        {
            var now = DateTime.UtcNow;
            var staleOperations = _operations.Values
                .Where(op => now - op.StartTime > _operationTimeout)
                .ToList();

            foreach (var operation in staleOperations)
            {
                _logger.LogWarning(
                    "Stale operation detected - ID: {OperationId}, Name: {OperationName}, Duration: {Duration}",
                    operation.Id,
                    operation.Name,
                    now - operation.StartTime);

                // Remove stale operation from tracking
                if (_operations.TryRemove(operation.Id, out _))
                {
                    operation.Cancel();
                    _operationThrottle.Release();
                }
            }
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
                // Expected when canceling monitoring task
            }

            // Cancel all tracked operations
            foreach (var operation in _operations.Values)
            {
                operation.Cancel();
            }
            _operations.Clear();

            _monitorCts.Dispose();
            _operationThrottle.Dispose();
        }

        public class TrackedOperation
        {
            public Guid Id { get; }
            public string Name { get; }
            public DateTime StartTime { get; private set; }
            public TimeSpan? Duration => Status != OperationStatus.Running ? 
                DateTime.UtcNow - StartTime : null;
            public OperationStatus Status { get; private set; }
            public Exception Error { get; private set; }

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

            public void Complete()
            {
                Status = OperationStatus.Completed;
            }

            public void Cancel()
            {
                Status = OperationStatus.Cancelled;
            }

            public void Fail(Exception error)
            {
                Error = error;
                Status = OperationStatus.Failed;
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
    }
}