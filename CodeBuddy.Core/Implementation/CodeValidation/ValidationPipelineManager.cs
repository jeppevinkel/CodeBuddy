using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Manages the concurrent validation pipeline with rate limiting and resource management capabilities.
    /// </summary>
    public class ValidationPipelineManager : IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, ValidationPipeline> _pipelines;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentPriorityQueue<ValidationRequest> _requestQueue;
        private readonly Task _queueProcessingTask;
        private readonly MetricsCollector _metricsCollector;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly BackoffManager _backoffManager;
        private readonly Timer _stalledValidationTimer;
        private readonly Timer _selfHealingTimer;
        private bool _disposed;

        // Configuration constants
        private const int DEFAULT_MAX_CONCURRENT_VALIDATIONS = 4;
        private const int DEFAULT_QUEUE_SIZE = 1000;
        private static readonly TimeSpan STALLED_VALIDATION_CHECK_INTERVAL = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan SELF_HEALING_INTERVAL = TimeSpan.FromMinutes(5);

        public ValidationPipelineManager(ILogger logger)
        {
            _logger = logger;
            _pipelines = new ConcurrentDictionary<string, ValidationPipeline>();
            _resourceMonitor = new ResourceMonitor(logger);
            _shutdownCts = new CancellationTokenSource();
            _concurrencyLimiter = new SemaphoreSlim(DEFAULT_MAX_CONCURRENT_VALIDATIONS);
            _requestQueue = new ConcurrentPriorityQueue<ValidationRequest>();
            _metricsCollector = new MetricsCollector(logger);
            _circuitBreaker = new CircuitBreaker(logger);
            _backoffManager = new BackoffManager(logger);
            
            // Start resource monitoring and queue processing
            _resourceMonitor.StartMonitoring();
            _queueProcessingTask = ProcessQueueAsync(_shutdownCts.Token);
            
            // Initialize cleanup timers
            _stalledValidationTimer = new Timer(
                CleanupStalledValidations, 
                null, 
                STALLED_VALIDATION_CHECK_INTERVAL, 
                STALLED_VALIDATION_CHECK_INTERVAL);
            
            _selfHealingTimer = new Timer(
                PerformSelfHealing, 
                null, 
                SELF_HEALING_INTERVAL, 
                SELF_HEALING_INTERVAL);
        }

        public PipelineMetrics GetMetrics() => _metricsCollector.GetCurrentMetrics();

        /// <summary>
        /// Submits a validation request to the pipeline
        /// </summary>
        public async Task<ValidationResult> SubmitValidationRequestAsync(
            string code,
            string language,
            ValidationOptions options,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Check circuit breaker
            if (!_circuitBreaker.AllowRequest())
            {
                throw new InvalidOperationException("Service is currently unavailable due to circuit breaker");
            }

            var validationId = Guid.NewGuid().ToString();
            var request = new ValidationRequest
            {
                Id = validationId,
                Code = code,
                Language = language,
                Options = options,
                Priority = CalculateRequestPriority(code, options),
                CompletionSource = new TaskCompletionSource<ValidationResult>(),
                CancellationToken = cancellationToken,
                SubmissionTime = DateTime.UtcNow
            };

            _metricsCollector.TrackValidationStart(validationId, code.Length);

            try
            {
                // Check if we can process immediately
                if (await TryProcessImmediatelyAsync(request))
                {
                    return await request.CompletionSource.Task;
                }

                // Queue the request if immediate processing not possible
                if (_requestQueue.Count >= DEFAULT_QUEUE_SIZE)
                {
                    throw new InvalidOperationException("Validation queue is full. Please try again later.");
                }

                _requestQueue.Enqueue(request, request.Priority);
                _logger.LogInformation(
                    "Request {ValidationId} queued with priority {Priority}. Current queue size: {QueueSize}", 
                    validationId, 
                    request.Priority,
                    _requestQueue.Count);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, 
                    _shutdownCts.Token);

                // Set up request timeout
                var timeoutTask = Task.Delay(GetRequestTimeout(request), linkedCts.Token);
                var completionTask = request.CompletionSource.Task;
                
                var completedTask = await Task.WhenAny(completionTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    request.CompletionSource.TrySetException(
                        new TimeoutException("Validation request timed out"));
                }

                var result = await request.CompletionSource.Task;
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                _metricsCollector.TrackValidationComplete(validationId, false);
                _circuitBreaker.RecordFailure();
                
                if (await _backoffManager.ShouldRetryAsync(validationId, ex))
                {
                    // Retry the request
                    return await SubmitValidationRequestAsync(code, language, options, cancellationToken);
                }
                
                throw;
            }
        }

        private int CalculateRequestPriority(string code, ValidationOptions options)
        {
            var priority = 0;
            
            // Lower number = higher priority
            priority += code.Length / 1000; // Longer code = lower priority
            
            // Critical validations get higher priority
            if (options.ValidateSecurity) priority -= 50;
            if (options.IsCritical) priority -= 100;
            
            // Optional validations get lower priority
            if (options.ValidateStyle) priority += 20;
            if (options.ValidateBestPractices) priority += 10;
            
            return priority;
        }

        private TimeSpan GetRequestTimeout(ValidationRequest request)
        {
            // Base timeout depends on code size
            var baseTimeout = TimeSpan.FromSeconds(30 + (request.Code.Length / 10000));
            
            // Add time for each validation type
            if (request.Options.ValidateSecurity) baseTimeout += TimeSpan.FromSeconds(30);
            if (request.Options.ValidateStyle) baseTimeout += TimeSpan.FromSeconds(15);
            if (request.Options.ValidateBestPractices) baseTimeout += TimeSpan.FromSeconds(15);
            
            return baseTimeout;
        }

        private async Task<bool> TryProcessImmediatelyAsync(ValidationRequest request)
        {
            // Check resource availability
            if (!_resourceMonitor.HasAvailableCapacity())
            {
                return false;
            }

            // Try to acquire concurrency slot
            if (!await _concurrencyLimiter.WaitAsync(0))
            {
                return false;
            }

            try
            {
                await ProcessValidationRequestAsync(request);
                return true;
            }
            catch
            {
                _concurrencyLimiter.Release();
                throw;
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for available resources
                    while (!_resourceMonitor.HasAvailableCapacity() && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    if (_requestQueue.TryDequeue(out var request))
                    {
                        // Skip expired requests
                        if (DateTime.UtcNow - request.SubmissionTime > GetRequestTimeout(request))
                        {
                            request.CompletionSource.TrySetException(
                                new TimeoutException("Request expired in queue"));
                            continue;
                        }

                        await _concurrencyLimiter.WaitAsync(cancellationToken);

                        _ = ProcessValidationRequestAsync(request)
                            .ContinueWith(t =>
                            {
                                _concurrencyLimiter.Release();
                                if (t.IsFaulted)
                                {
                                    request.CompletionSource.TrySetException(t.Exception.InnerExceptions);
                                }
                            }, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing validation queue");
                    await Task.Delay(1000, cancellationToken); // Back off on error
                }
            }
        }

        private async Task ProcessValidationRequestAsync(ValidationRequest request)
        {
            try
            {
                // Get or create pipeline for this language
                var pipeline = _pipelines.GetOrAdd(request.Language, _ => new ValidationPipeline(_logger));

                // Execute validation through pipeline
                var result = await pipeline.ValidateAsync(
                    request.Code,
                    request.Options,
                    request.CancellationToken);

                _metricsCollector.TrackValidationComplete(request.Id, true);
                request.CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing validation request {ValidationId}", request.Id);
                _metricsCollector.TrackValidationComplete(request.Id, false);
                request.CompletionSource.TrySetException(ex);
                throw;
            }
        }

        private void CleanupStalledValidations(object state)
        {
            var metrics = _metricsCollector.GetCurrentMetrics();
            if (metrics.StalledValidations > 0)
            {
                _logger.LogWarning(
                    "Detected {StalledCount} stalled validations. Initiating cleanup...",
                    metrics.StalledValidations);

                foreach (var pipeline in _pipelines.Values)
                {
                    pipeline.CleanupStalled();
                }
            }
        }

        private void PerformSelfHealing(object state)
        {
            try
            {
                var metrics = _metricsCollector.GetCurrentMetrics();
                
                // Reset circuit breaker if conditions are good
                if (metrics.CpuUsagePercent < 70 && 
                    metrics.MemoryUsageBytes < 700 * 1024 * 1024 && // 700MB
                    metrics.QueuedRequests < DEFAULT_QUEUE_SIZE / 2)
                {
                    _circuitBreaker.RecordSuccess();
                }

                // Adjust concurrency limit based on performance
                var currentLimit = _concurrencyLimiter.CurrentCount;
                if (metrics.CpuUsagePercent < 60 && currentLimit < 8)
                {
                    // Gradually increase concurrency
                    _concurrencyLimiter.Release();
                    _logger.LogInformation("Increased concurrency limit to {NewLimit}", currentLimit + 1);
                }
                else if (metrics.CpuUsagePercent > 80 && currentLimit > 2)
                {
                    // Reduce concurrency
                    _concurrencyLimiter.Wait(0);
                    _logger.LogInformation("Decreased concurrency limit to {NewLimit}", currentLimit - 1);
                }

                // Cleanup any orphaned resources
                GC.Collect(0, GCCollectionMode.Optimized, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during self-healing");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _shutdownCts.Cancel();
            _resourceMonitor?.Dispose();
            _metricsCollector?.Dispose();
            _concurrencyLimiter?.Dispose();
            _stalledValidationTimer?.Dispose();
            _selfHealingTimer?.Dispose();
            _shutdownCts?.Dispose();

            foreach (var pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }

            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _shutdownCts.Cancel();
            try
            {
                // Wait for queue processing to complete
                if (_queueProcessingTask != null)
                {
                    await _queueProcessingTask.WaitAsync(TimeSpan.FromSeconds(5));
                }

                foreach (var pipeline in _pipelines.Values)
                {
                    await pipeline.DisposeAsync();
                }
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Queue processing task did not complete within timeout during shutdown");
            }

            _resourceMonitor?.Dispose();
            _metricsCollector?.Dispose();
            _concurrencyLimiter?.Dispose();
            _stalledValidationTimer?.Dispose();
            _selfHealingTimer?.Dispose();
            _shutdownCts?.Dispose();

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ValidationPipelineManager));
            }
        }

        private class ValidationRequest
        {
            public string Id { get; set; }
            public string Code { get; set; }
            public string Language { get; set; }
            public ValidationOptions Options { get; set; }
            public int Priority { get; set; }
            public TaskCompletionSource<ValidationResult> CompletionSource { get; set; }
            public CancellationToken CancellationToken { get; set; }
            public DateTime SubmissionTime { get; set; }
        }
    }
}