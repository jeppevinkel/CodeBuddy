using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidationPipeline
{
    private readonly ILogger<ValidationPipeline> _logger;
    private readonly List<IValidationMiddleware> _middleware;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;
    private readonly ConcurrentDictionary<string, MiddlewareMetrics> _metrics;
    private readonly ValidationResilienceConfig _config;
    private readonly IMetricsAggregator _metricsAggregator;

    public ValidationPipeline(
        ILogger<ValidationPipeline> logger,
        ValidationResilienceConfig config,
        IMetricsAggregator metricsAggregator)
    {
        _logger = logger;
        _middleware = new List<IValidationMiddleware>();
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
        _metrics = new ConcurrentDictionary<string, MiddlewareMetrics>();
        _config = config;
        _metricsAggregator = metricsAggregator;
    }

    public void AddMiddleware(IValidationMiddleware middleware)
    {
        _middleware.Add(middleware);
        _middleware.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public async Task<ValidationResult> ExecuteAsync(ValidationContext context)
    {
        var result = new ValidationResult
        {
            State = ValidationState.InProgress,
            Language = context.Language
        };

        try
        {
            ValidationDelegate pipeline = BuildPipeline(context);
            var pipelineResult = await pipeline(context);
            
            // Merge pipeline result with our tracking
            result.IsValid = pipelineResult.IsValid;
            result.Issues = pipelineResult.Issues;
            result.Statistics = pipelineResult.Statistics;
            
            result.State = result.FailedMiddleware.Any() 
                ? ValidationState.CompletedWithErrors 
                : ValidationState.Completed;
            
            result.IsPartialSuccess = result.FailedMiddleware.Any() && result.IsValid;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation pipeline failed");
            result.State = ValidationState.Failed;
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Validation pipeline failed: " + ex.Message
            });
            return result;
        }
    }

    private ValidationDelegate BuildPipeline(ValidationContext context)
    {
        ValidationDelegate pipeline = async (ctx) =>
        {
            return await context.Validator.ValidateAsync(ctx.Code, ctx.Language, ctx.Options);
        };

        // Build the pipeline in reverse order
        for (int i = _middleware.Count - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = pipeline;
            pipeline = async (ctx) =>
            {
                if (ShouldSkipMiddleware(middleware.Name))
                {
                    _logger.LogWarning("Skipping middleware {MiddlewareName} due to circuit breaker", middleware.Name);
                    ctx.Result.SkippedMiddleware.Add(middleware.Name);
                    return await next(ctx);
                }

                var middlewareConfig = GetMiddlewareConfig(middleware.Name);
                var attempts = 0;
                var startTime = DateTime.UtcNow;

                while (attempts <= middlewareConfig.MaxRetryAttempts)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(middlewareConfig.Timeout ?? _config.MiddlewareTimeout);
                        var result = await middleware.ProcessAsync(ctx, next).WaitAsync(cts.Token);
                        
                        // Success - reset failure count
                        ResetFailureCount(middleware.Name);
                        var duration = DateTime.UtcNow - startTime;
                        RecordSuccess(middleware.Name, duration);
                        _metricsAggregator.RecordMiddlewareExecution(middleware.Name, true, duration);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        var duration = DateTime.UtcNow - startTime;
                        var failure = RecordFailure(middleware.Name, ex);
                        _metricsAggregator.RecordMiddlewareExecution(middleware.Name, false, duration);
                        _metricsAggregator.RecordRetryAttempt(middleware.Name);
                        ctx.Result.FailedMiddleware.Add(failure);

                        if (IsCircuitBreakerTripped(middleware.Name))
                        {
                            _logger.LogError("Circuit breaker tripped for {MiddlewareName}", middleware.Name);
                            if (middlewareConfig.EnableFallback ?? _config.EnableFallbackBehavior)
                            {
                                return await next(ctx); // Fallback to skipping this middleware
                            }
                            throw;
                        }

                        if (attempts > middlewareConfig.MaxRetryAttempts)
                        {
                            _logger.LogError(ex, "Max retry attempts ({Attempts}) reached for {MiddlewareName}", 
                                attempts, middleware.Name);
                            
                            if (_config.ContinueOnMiddlewareFailure)
                            {
                                return await next(ctx);
                            }
                            throw;
                        }

                        // Apply retry delay based on strategy
                        await ApplyRetryDelay(attempts, middlewareConfig.RetryStrategy ?? _config.RetryStrategy);
                    }
                }

                throw new InvalidOperationException("Should not reach this point");
            };
        }

        return pipeline;
    }

    private MiddlewareResilienceConfig GetMiddlewareConfig(string middlewareName)
    {
        if (_config.MiddlewareSpecificConfig.TryGetValue(middlewareName, out var config))
        {
            return new MiddlewareResilienceConfig
            {
                MaxRetryAttempts = config.MaxRetryAttempts ?? _config.MaxRetryAttempts,
                CircuitBreakerThreshold = config.CircuitBreakerThreshold ?? _config.CircuitBreakerThreshold,
                Timeout = config.Timeout ?? _config.MiddlewareTimeout,
                EnableFallback = config.EnableFallback ?? _config.EnableFallbackBehavior,
                RetryStrategy = config.RetryStrategy ?? _config.RetryStrategy,
                FallbackAction = config.FallbackAction
            };
        }

        return new MiddlewareResilienceConfig
        {
            MaxRetryAttempts = _config.MaxRetryAttempts,
            CircuitBreakerThreshold = _config.CircuitBreakerThreshold,
            Timeout = _config.MiddlewareTimeout,
            EnableFallback = _config.EnableFallbackBehavior,
            RetryStrategy = _config.RetryStrategy
        };
    }

    private bool ShouldSkipMiddleware(string middlewareName)
    {
        return _circuitBreakers.TryGetValue(middlewareName, out var state) &&
               state.IsOpen &&
               DateTime.UtcNow < state.ResetTime;
    }

    private void ResetFailureCount(string middlewareName)
    {
        _circuitBreakers.TryRemove(middlewareName, out _);
        _metrics.AddOrUpdate(middlewareName,
            _ => new MiddlewareMetrics(),
            (_, metrics) =>
            {
                metrics.ConsecutiveFailures = 0;
                return metrics;
            });
    }

    private MiddlewareFailure RecordFailure(string middlewareName, Exception ex)
    {
        var metrics = _metrics.AddOrUpdate(middlewareName,
            _ => new MiddlewareMetrics { ConsecutiveFailures = 1 },
            (_, m) =>
            {
                m.ConsecutiveFailures++;
                m.TotalFailures++;
                return m;
            });

        var state = _circuitBreakers.AddOrUpdate(middlewareName,
            _ => new CircuitBreakerState
            {
                FailureCount = 1,
                IsOpen = false,
                LastFailure = DateTime.UtcNow
            },
            (_, s) =>
            {
                s.FailureCount++;
                s.LastFailure = DateTime.UtcNow;
                if (s.FailureCount >= _config.CircuitBreakerThreshold)
                {
                    s.IsOpen = true;
                    s.ResetTime = DateTime.UtcNow.Add(_config.CircuitBreakerResetTime);
                    _metricsAggregator.RecordCircuitBreakerStatus(middlewareName, true);
                }
                return s;
            });

        return new MiddlewareFailure
        {
            MiddlewareName = middlewareName,
            ErrorMessage = ex.Message,
            StackTrace = ex.StackTrace,
            FailureCount = metrics.ConsecutiveFailures,
            RetryAttempts = metrics.TotalFailures,
            CircuitBreakerTripped = state.IsOpen,
            Context = new Dictionary<string, string>
            {
                ["LastFailure"] = state.LastFailure.ToString("O"),
                ["ResetTime"] = state.ResetTime?.ToString("O")
            }
        };
    }

    private void RecordSuccess(string middlewareName, TimeSpan duration)
    {
        _metrics.AddOrUpdate(middlewareName,
            _ => new MiddlewareMetrics { LastSuccessfulDuration = duration },
            (_, metrics) =>
            {
                metrics.LastSuccessfulDuration = duration;
                metrics.TotalSuccesses++;
                return metrics;
            });
    }

    private bool IsCircuitBreakerTripped(string middlewareName)
    {
        return _circuitBreakers.TryGetValue(middlewareName, out var state) &&
               state.IsOpen &&
               DateTime.UtcNow < state.ResetTime;
    }

    private async Task ApplyRetryDelay(int attempt, RetryStrategy strategy)
    {
        var delay = strategy switch
        {
            RetryStrategy.Immediate => TimeSpan.Zero,
            RetryStrategy.LinearBackoff => TimeSpan.FromSeconds(attempt),
            RetryStrategy.ExponentialBackoff => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
            _ => TimeSpan.FromSeconds(attempt)
        };

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }
    }

    private class CircuitBreakerState
    {
        public int FailureCount { get; set; }
        public bool IsOpen { get; set; }
        public DateTime LastFailure { get; set; }
        public DateTime? ResetTime { get; set; }
    }

    private class MiddlewareMetrics
    {
        public int ConsecutiveFailures { get; set; }
        public int TotalFailures { get; set; }
        public int TotalSuccesses { get; set; }
        public TimeSpan LastSuccessfulDuration { get; set; }
    }

    private void EmitResourceMetrics()
    {
        var metrics = new ResourceMetrics
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsageMB = GetMemoryUsage(),
            DiskIoMBPS = GetDiskIoRate(),
            ActiveThreads = GetActiveThreadCount()
        };
        _metricsAggregator.RecordResourceUtilization(metrics);
    }

    private double GetCpuUsage()
    {
        return System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds /
               (Environment.ProcessorCount * DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds) * 100;
    }

    private double GetMemoryUsage()
    {
        return System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
    }

    private double GetDiskIoRate()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return (process.ReadBytes + process.WriteBytes) / (1024.0 * 1024.0);
    }

    private int GetActiveThreadCount()
    {
        return System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
    }
}