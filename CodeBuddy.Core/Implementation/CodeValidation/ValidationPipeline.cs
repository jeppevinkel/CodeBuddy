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
    private readonly IResourceAlertManager _alertManager;
    private readonly IResourceAnalytics _resourceAnalytics;
    
    // Resource throttling state
    private readonly SemaphoreSlim _validationThrottle;
    private readonly ConcurrentQueue<ResourceSnapshot> _resourceHistory;
    private readonly ConcurrentDictionary<string, int> _criticalValidationReservations;
    private volatile bool _isThrottled;
    private DateTime _lastThrottlingAdjustment;

    public ValidationPipeline(
        ILogger<ValidationPipeline> logger,
        ValidationResilienceConfig config,
        IMetricsAggregator metricsAggregator,
        IResourceAlertManager alertManager,
        IResourceAnalytics resourceAnalytics)
    {
        _logger = logger;
        _middleware = new List<IValidationMiddleware>();
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
        _metrics = new ConcurrentDictionary<string, MiddlewareMetrics>();
        _config = config;
        _metricsAggregator = metricsAggregator;
        _alertManager = alertManager;
        _resourceAnalytics = resourceAnalytics;
        
        // Initialize resource throttling
        _validationThrottle = new SemaphoreSlim(config.MaxConcurrentValidations);
        _resourceHistory = new ConcurrentQueue<ResourceSnapshot>();
        _criticalValidationReservations = new ConcurrentDictionary<string, int>();
        _lastThrottlingAdjustment = DateTime.UtcNow;
        
        // Start resource monitoring
        StartResourceMonitoring();
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
            // Apply resource throttling
            if (!await AcquireResourcesAsync(context))
            {
                result.State = ValidationState.Failed;
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Validation rejected due to resource constraints"
                });
                return result;
            }
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
        finally
        {
            // Release resources
            ReleaseResources(context);
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

    private class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public ResourceMetrics Metrics { get; set; }
    }

    private class ResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskIoMBPS { get; set; }
        public int ActiveThreads { get; set; }
        public double MemoryAllocationRate { get; set; }
        public double GCCollectionFrequency { get; set; }
        public double ThreadPoolUtilization { get; set; }
        public double DiskQueueLength { get; set; }
        public double NetworkBandwidthUsage { get; set; }
        public int PendingValidations { get; set; }
        public double ValidationThroughput { get; set; }
        public double AverageValidationLatency { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new();
    }

    private void StartResourceMonitoring()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                EmitResourceMetrics();
                UpdateResourceHistory();
                AdjustThrottlingParameters();
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        });
    }

    private async Task<bool> AcquireResourcesAsync(ValidationContext context)
    {
        if (!await _validationThrottle.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            _logger.LogWarning("Failed to acquire validation slot - max concurrent validations reached");
            return false;
        }

        if (_isThrottled && !context.IsCriticalValidation)
        {
            _validationThrottle.Release();
            _logger.LogWarning("Validation rejected due to resource throttling");
            return false;
        }

        var metrics = GetCurrentResourceMetrics();
        if (ExceedsResourceThresholds(metrics) && !context.IsCriticalValidation)
        {
            _validationThrottle.Release();
            _logger.LogWarning("Validation rejected due to resource constraints: CPU {CpuUsage}%, Memory {MemoryUsage}MB, IO {IoRate}MBPS",
                metrics.CpuUsagePercent, metrics.MemoryUsageMB, metrics.DiskIoMBPS);
            return false;
        }

        if (context.IsCriticalValidation)
        {
            _criticalValidationReservations.AddOrUpdate(context.Id, 1, (_, count) => count + 1);
        }

        return true;
    }

    private void ReleaseResources(ValidationContext context)
    {
        _validationThrottle.Release();
        
        if (context.IsCriticalValidation)
        {
            _criticalValidationReservations.AddOrUpdate(context.Id, 0, (_, count) => Math.Max(0, count - 1));
        }
    }

    private bool ExceedsResourceThresholds(ResourceMetrics metrics)
    {
        return metrics.CpuUsagePercent > _config.MaxCpuThresholdPercent ||
               metrics.MemoryUsageMB > _config.MaxMemoryThresholdMB ||
               metrics.DiskIoMBPS > _config.MaxDiskIoMBPS;
    }

    private void UpdateResourceHistory()
    {
        var snapshot = new ResourceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Metrics = GetCurrentResourceMetrics()
        };

        _resourceHistory.Enqueue(snapshot);

        // Trim old entries
        while (_resourceHistory.TryPeek(out var oldest) &&
               (DateTime.UtcNow - oldest.Timestamp) > _config.ResourceTrendInterval)
        {
            _resourceHistory.TryDequeue(out _);
        }
    }

    private void AdjustThrottlingParameters()
    {
        if (!_config.EnableAdaptiveThrottling ||
            (DateTime.UtcNow - _lastThrottlingAdjustment) < TimeSpan.FromMinutes(1))
        {
            return;
        }

        var metrics = _resourceHistory.ToList();
        if (metrics.Count < 2)
        {
            return;
        }

        var trend = CalculateResourceTrend(metrics);
        var currentReservations = _criticalValidationReservations.Values.Sum();
        var reservationLimit = (int)(_config.MaxConcurrentValidations * (_config.ResourceReservationPercent / 100.0));

        if (trend > 0.1 && currentReservations < reservationLimit) // Resource usage trending up
        {
            var newLimit = Math.Max(1, (int)(_config.MaxConcurrentValidations * (1 - _config.ThrottlingAdjustmentFactor)));
            _validationThrottle.Release(_validationThrottle.CurrentCount);
            _validationThrottle = new SemaphoreSlim(newLimit);
            _isThrottled = true;
            _logger.LogInformation("Throttling increased - new concurrent limit: {Limit}", newLimit);
        }
        else if (trend < -0.1 && _isThrottled) // Resource usage trending down
        {
            var newLimit = Math.Min(_config.MaxConcurrentValidations,
                (int)(_config.MaxConcurrentValidations * (1 + _config.ThrottlingAdjustmentFactor)));
            _validationThrottle.Release(_validationThrottle.CurrentCount);
            _validationThrottle = new SemaphoreSlim(newLimit);
            _isThrottled = false;
            _logger.LogInformation("Throttling decreased - new concurrent limit: {Limit}", newLimit);
        }

        _lastThrottlingAdjustment = DateTime.UtcNow;
    }

    private double CalculateResourceTrend(List<ResourceSnapshot> metrics)
    {
        // Simple linear regression on CPU usage as primary indicator
        var n = metrics.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumXX = 0.0;

        for (var i = 0; i < n; i++)
        {
            var x = i;
            var y = metrics[i].Metrics.CpuUsagePercent;
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        return slope;
    }

    private ResourceMetrics GetCurrentResourceMetrics()
    {
        return new ResourceMetrics
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsageMB = GetMemoryUsage(),
            DiskIoMBPS = GetDiskIoRate(),
            ActiveThreads = GetActiveThreadCount()
        };
    }

    private async void EmitResourceMetrics()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var metrics = new ResourceMetrics
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsageMB = GetMemoryUsage(),
            DiskIoMBPS = GetDiskIoRate(),
            ActiveThreads = GetActiveThreadCount(),
            MemoryAllocationRate = CalculateMemoryAllocationRate(),
            GCCollectionFrequency = CalculateGCFrequency(),
            ThreadPoolUtilization = GetThreadPoolUtilization(),
            DiskQueueLength = GetDiskQueueLength(),
            NetworkBandwidthUsage = GetNetworkBandwidthUsage(),
            PendingValidations = _validationThrottle.CurrentCount,
            ValidationThroughput = CalculateValidationThroughput(),
            AverageValidationLatency = CalculateAverageValidationLatency(),
            CustomMetrics = CollectCustomMetrics()
        };
        _metricsAggregator.RecordResourceUtilization(metrics);

        // Build comprehensive metrics for alert manager
        var metricMap = new Dictionary<ResourceMetricType, double>
        {
            [ResourceMetricType.CPU] = metrics.CpuUsagePercent,
            [ResourceMetricType.Memory] = metrics.MemoryUsageMB,
            [ResourceMetricType.DiskIO] = metrics.DiskIoMBPS,
            [ResourceMetricType.ThreadPool] = metrics.ThreadPoolUtilization,
            [ResourceMetricType.GC] = metrics.GCCollectionFrequency,
            [ResourceMetricType.Network] = metrics.NetworkBandwidthUsage,
            [ResourceMetricType.ValidationThroughput] = metrics.ValidationThroughput,
            [ResourceMetricType.ValidationLatency] = metrics.AverageValidationLatency
        };

        // Send metrics to alert manager for analysis with detailed context
        await _alertManager.ProcessMetricsAsync(metricMap, "ValidationPipeline");

        // Store detailed metrics in analytics system
        var usageData = new ResourceUsageData
        {
            PipelineId = "ValidationPipeline",
            ValidatorType = "System",
            CpuUsagePercentage = metrics.CpuUsagePercent,
            MemoryUsageMB = metrics.MemoryUsageMB,
            DiskIOBytesPerSecond = metrics.DiskIoMBPS * 1024 * 1024,
            ThreadPoolUtilization = metrics.ThreadPoolUtilization,
            GCCollectionFrequency = metrics.GCCollectionFrequency,
            NetworkBandwidthUsage = metrics.NetworkBandwidthUsage,
            ValidationThroughput = metrics.ValidationThroughput,
            ValidationLatency = metrics.AverageValidationLatency,
            ActiveThreadCount = metrics.ActiveThreads,
            PendingValidations = metrics.PendingValidations,
            CustomMetrics = metrics.CustomMetrics,
            Timestamp = DateTime.UtcNow
        };

        await _resourceAnalytics.StoreResourceUsageDataAsync(usageData);

        // Perform comprehensive bottleneck analysis
        var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync(new AnalysisOptions
        {
            IncludeHistoricalTrends = true,
            LookbackPeriod = TimeSpan.FromHours(24),
            AnalysisGranularity = TimeSpan.FromMinutes(5),
            ResourceTypes = new[]
            {
                ResourceMetricType.CPU,
                ResourceMetricType.Memory,
                ResourceMetricType.DiskIO,
                ResourceMetricType.ThreadPool,
                ResourceMetricType.GC,
                ResourceMetricType.Network,
                ResourceMetricType.ValidationThroughput,
                ResourceMetricType.ValidationLatency
            }
        });
        foreach (var bottleneck in bottlenecks)
        {
            var severity = DetermineAlertSeverity(bottleneck);
            await _alertManager.RaiseResourceAlert(new ResourceAlert
            {
                ResourceType = bottleneck.ResourceType,
                Severity = severity,
                Message = $"Resource bottleneck detected: {bottleneck.Impact}",
                RecommendedAction = bottleneck.RecommendedAction,
                TrendData = bottleneck.TrendAnalysis,
                ProjectedTimeToThreshold = bottleneck.ProjectedTimeToThreshold,
                AffectedComponents = bottleneck.AffectedComponents,
                MitigationSteps = bottleneck.MitigationSteps,
                HistoricalContext = bottleneck.HistoricalContext
            });
        }
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

    private double CalculateMemoryAllocationRate()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var initialMemory = process.PrivateMemorySize64;
        Thread.Sleep(100); // Sample over 100ms
        var finalMemory = process.PrivateMemorySize64;
        return (finalMemory - initialMemory) / (1024.0 * 1024.0 * 0.1); // MB/s
    }

    private double CalculateGCFrequency()
    {
        var gcCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return gcCount / uptime.TotalMinutes; // Collections per minute
    }

    private double GetThreadPoolUtilization()
    {
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        
        var workerThreadUtilization = (maxWorkerThreads - workerThreads) / (double)maxWorkerThreads * 100;
        var ioThreadUtilization = (maxCompletionPortThreads - completionPortThreads) / (double)maxCompletionPortThreads * 100;
        
        return Math.Max(workerThreadUtilization, ioThreadUtilization);
    }

    private double GetDiskQueueLength()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            var systemDrive = drives.FirstOrDefault(d => d.IsReady && d.RootDirectory.FullName == Path.GetPathRoot(Environment.SystemDirectory));
            if (systemDrive != null)
            {
                // This is a simplified approximation - in production you'd use performance counters
                return systemDrive.AvailableFreeSpace < 1024 * 1024 * 100 ? 1.0 : 0.0; // Simple threshold at 100MB
            }
            return 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private double GetNetworkBandwidthUsage()
    {
        var process = Process.GetCurrentProcess();
        // This is a simplified version - in production you'd use performance counters
        return (process.ReadBytes + process.WriteBytes) / (1024.0 * 1024.0); // MB/s
    }

    private double CalculateValidationThroughput()
    {
        var recentMetrics = _resourceHistory
            .Where(m => (DateTime.UtcNow - m.Timestamp) <= TimeSpan.FromMinutes(1))
            .ToList();

        if (recentMetrics.Count < 2) return 0;

        var periodSeconds = (recentMetrics.Last().Timestamp - recentMetrics.First().Timestamp).TotalSeconds;
        if (periodSeconds <= 0) return 0;

        return _metrics.Values.Sum(m => m.TotalSuccesses) / periodSeconds;
    }

    private double CalculateAverageValidationLatency()
    {
        var recentMetrics = _metrics.Values
            .Where(m => m.LastSuccessfulDuration > TimeSpan.Zero)
            .Select(m => m.LastSuccessfulDuration.TotalMilliseconds)
            .ToList();

        return recentMetrics.Any() ? recentMetrics.Average() : 0;
    }

    private Dictionary<string, double> CollectCustomMetrics()
    {
        return new Dictionary<string, double>
        {
            ["validation_queue_depth"] = _validationThrottle.CurrentCount,
            ["critical_validations"] = _criticalValidationReservations.Values.Sum(),
            ["throttling_level"] = _isThrottled ? 1.0 : 0.0,
            ["resource_trend"] = CalculateResourceTrend(_resourceHistory.ToList())
        };
    }

    private AlertSeverity DetermineAlertSeverity(ResourceBottleneck bottleneck)
    {
        if (bottleneck.ImpactLevel >= 0.8) return AlertSeverity.Critical;
        if (bottleneck.ImpactLevel >= 0.6) return AlertSeverity.High;
        if (bottleneck.ImpactLevel >= 0.4) return AlertSeverity.Medium;
        if (bottleneck.ImpactLevel >= 0.2) return AlertSeverity.Warning;
        return AlertSeverity.Info;
    }
}