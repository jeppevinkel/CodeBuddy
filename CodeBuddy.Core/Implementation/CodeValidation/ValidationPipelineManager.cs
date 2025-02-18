using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
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
        private readonly ConcurrentQueue<ValidationRequest> _requestQueue;
        private readonly Task _queueProcessingTask;
        private bool _disposed;

        // Configuration constants
        private const int DEFAULT_MAX_CONCURRENT_VALIDATIONS = 4;
        private const int DEFAULT_QUEUE_SIZE = 1000;
        private const double CPU_THRESHOLD_PERCENT = 80.0;
        private const long MEMORY_THRESHOLD_BYTES = 800 * 1024 * 1024; // 800MB

        public ValidationPipelineManager(ILogger logger)
        {
            _logger = logger;
            _pipelines = new ConcurrentDictionary<string, ValidationPipeline>();
            _resourceMonitor = new ResourceMonitor(logger);
            _shutdownCts = new CancellationTokenSource();
            _concurrencyLimiter = new SemaphoreSlim(DEFAULT_MAX_CONCURRENT_VALIDATIONS);
            _requestQueue = new ConcurrentQueue<ValidationRequest>();
            
            // Start resource monitoring and queue processing
            _resourceMonitor.StartMonitoring();
            _queueProcessingTask = ProcessQueueAsync(_shutdownCts.Token);
        }

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

            var request = new ValidationRequest
            {
                Code = code,
                Language = language,
                Options = options,
                CompletionSource = new TaskCompletionSource<ValidationResult>(),
                CancellationToken = cancellationToken
            };

            // Check if we can process immediately or need to queue
            if (await TryProcessImmediatelyAsync(request))
            {
                return await request.CompletionSource.Task;
            }

            // Queue the request if immediate processing not possible
            if (_requestQueue.Count >= DEFAULT_QUEUE_SIZE)
            {
                throw new InvalidOperationException("Validation queue is full. Please try again later.");
            }

            _requestQueue.Enqueue(request);
            _logger.LogInformation("Request queued. Current queue size: {QueueSize}", _requestQueue.Count);

            return await request.CompletionSource.Task;
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
            catch (Exception)
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

                request.CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing validation request");
                request.CompletionSource.TrySetException(ex);
                throw;
            }
        }

        /// <summary>
        /// Monitors system resources and provides capacity information
        /// </summary>
        private class ResourceMonitor
        {
            private readonly ILogger _logger;
            private readonly PerformanceCounter _cpuCounter;
            private readonly Process _currentProcess;
            private Task _monitoringTask;
            private CancellationTokenSource _monitoringCts;
            private volatile bool _hasAvailableCapacity = true;

            public ResourceMonitor(ILogger logger)
            {
                _logger = logger;
                _currentProcess = Process.GetCurrentProcess();
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }

            public void StartMonitoring()
            {
                _monitoringCts = new CancellationTokenSource();
                _monitoringTask = MonitorResourcesAsync(_monitoringCts.Token);
            }

            public bool HasAvailableCapacity() => _hasAvailableCapacity;

            private async Task MonitorResourcesAsync(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var cpuUsage = _cpuCounter.NextValue();
                        var memoryUsage = _currentProcess.WorkingSet64;

                        _hasAvailableCapacity = cpuUsage < CPU_THRESHOLD_PERCENT 
                            && memoryUsage < MEMORY_THRESHOLD_BYTES;

                        if (!_hasAvailableCapacity)
                        {
                            _logger.LogWarning(
                                "Resource limits reached - CPU: {CpuUsage}%, Memory: {MemoryUsageMB}MB",
                                cpuUsage,
                                memoryUsage / (1024 * 1024));
                        }

                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error monitoring resources");
                        await Task.Delay(5000, cancellationToken); // Back off on error
                    }
                }
            }

            public void Dispose()
            {
                _monitoringCts?.Cancel();
                _monitoringCts?.Dispose();
                _cpuCounter?.Dispose();
            }
        }

        /// <summary>
        /// Manages validation pipeline for a specific language
        /// </summary>
        private class ValidationPipeline
        {
            private readonly ILogger _logger;
            private readonly ICodeValidator _validator;
            private long _totalValidations;
            private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceHistory;

            public ValidationPipeline(ILogger logger)
            {
                _logger = logger;
                _validator = CodeValidatorFactory.CreateValidator(logger);
                _performanceHistory = new ConcurrentDictionary<string, PerformanceMetrics>();
            }

            public async Task<ValidationResult> ValidateAsync(
                string code,
                ValidationOptions options,
                CancellationToken cancellationToken)
            {
                var validationId = Interlocked.Increment(ref _totalValidations).ToString();
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await _validator.ValidateAsync(code, options, cancellationToken);
                    
                    // Record performance metrics
                    stopwatch.Stop();
                    var metrics = new PerformanceMetrics
                    {
                        ValidationId = validationId,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        CodeSizeBytes = code.Length,
                        Timestamp = DateTime.UtcNow
                    };
                    _performanceHistory.TryAdd(validationId, metrics);

                    // Cleanup old history
                    CleanupOldHistory();

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Validation failed for ID {ValidationId}", validationId);
                    throw;
                }
            }

            private void CleanupOldHistory()
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);
                foreach (var oldMetric in _performanceHistory.Where(x => x.Value.Timestamp < cutoff))
                {
                    _performanceHistory.TryRemove(oldMetric.Key, out _);
                }
            }
        }

        private class ValidationRequest
        {
            public string Code { get; set; }
            public string Language { get; set; }
            public ValidationOptions Options { get; set; }
            public TaskCompletionSource<ValidationResult> CompletionSource { get; set; }
            public CancellationToken CancellationToken { get; set; }
        }

        private class PerformanceMetrics
        {
            public string ValidationId { get; set; }
            public long ExecutionTimeMs { get; set; }
            public long CodeSizeBytes { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _shutdownCts.Cancel();
            _resourceMonitor?.Dispose();
            _concurrencyLimiter?.Dispose();
            _shutdownCts?.Dispose();

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
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Queue processing task did not complete within timeout during shutdown");
            }

            _resourceMonitor?.Dispose();
            _concurrencyLimiter?.Dispose();
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
    }
}