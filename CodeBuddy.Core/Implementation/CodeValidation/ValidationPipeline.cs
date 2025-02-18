using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Coverage;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidationPipeline : IDisposable
{
    private readonly ILogger<ValidationPipeline> _logger;
    private readonly List<IValidationMiddleware> _middleware;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;
    private readonly ConcurrentDictionary<string, MiddlewareMetrics> _metrics;
    private readonly ValidationResilienceConfig _config;
    private readonly IMetricsAggregator _metricsAggregator;
    private readonly IResourceAlertManager _alertManager;
    private readonly IResourceAnalytics _resourceAnalytics;
    private readonly IDistributedCache _distributedCache;
    private readonly IClusterCoordinator _clusterCoordinator;
    private readonly ILoadBalancer _loadBalancer;
    private readonly IDistributedTracer _distributedTracer;
    private readonly IHealthMonitor _healthMonitor;
    private readonly string _nodeId;
    private bool _isLeader;
    private Timer _leaderElectionTimer;
    
    // Resource throttling state
    private readonly SemaphoreSlim _validationThrottle;
    private readonly ConcurrentQueue<ResourceSnapshot> _resourceHistory;
    private readonly ITestCoverageGenerator TestCoverageGenerator;
    private readonly ConcurrentDictionary<string, int> _criticalValidationReservations;
    private volatile bool _isThrottled;
    private DateTime _lastThrottlingAdjustment;

    public ValidationPipeline(
        ILogger<ValidationPipeline> logger,
        ValidationResilienceConfig config,
        IMetricsAggregator metricsAggregator,
        IResourceAlertManager alertManager,
        IResourceAnalytics resourceAnalytics,
        IDistributedCache distributedCache,
        IClusterCoordinator clusterCoordinator,
        ILoadBalancer loadBalancer,
        IDistributedTracer distributedTracer,
        IHealthMonitor healthMonitor,
        ITestCoverageGenerator testCoverageGenerator)
    {
        _logger = logger;
        _middleware = new List<IValidationMiddleware>();
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
        _metrics = new ConcurrentDictionary<string, MiddlewareMetrics>();
        _config = config;
        _metricsAggregator = metricsAggregator;
        _alertManager = alertManager;
        _resourceAnalytics = resourceAnalytics;
        _distributedCache = distributedCache;
        _clusterCoordinator = clusterCoordinator;
        _loadBalancer = loadBalancer;
        _distributedTracer = distributedTracer;
        _healthMonitor = healthMonitor;
        TestCoverageGenerator = testCoverageGenerator;
        
        // Generate unique node ID
        _nodeId = GenerateNodeId();
        
        // Initialize resource throttling
        _validationThrottle = new SemaphoreSlim(config.MaxConcurrentValidations);
        _resourceHistory = new ConcurrentQueue<ResourceSnapshot>();
        _criticalValidationReservations = new ConcurrentDictionary<string, int>();
        _lastThrottlingAdjustment = DateTime.UtcNow;
        
        // Start cluster coordination
        InitializeClusterNode();
    }

    public void AddMiddleware(IValidationMiddleware middleware)
    {
        _middleware.Add(middleware);
        _middleware.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public async Task<ValidationResult> ExecuteAsync(ValidationContext context)
    {
        using var trace = _distributedTracer.StartTrace("ValidationPipeline.Execute");
        trace.SetTag("nodeId", _nodeId);
        
        // Try to get result from distributed cache first
        var codeHash = ComputeCodeHash(context.Code);
        var (found, cachedResult) = await _distributedCache.TryGetAsync(codeHash, context.Options);
        
        if (found)
        {
            trace.SetTag("cache_hit", true);
            _logger.LogInformation("Validation result found in distributed cache for code hash: {CodeHash}", codeHash);
            return cachedResult;
        }

        // Check if we should handle this validation or forward to another node
        if (!await ShouldHandleValidation(context))
        {
            trace.SetTag("forwarded", true);
            var targetNode = await _loadBalancer.SelectNodeAsync(context);
            return await ForwardValidationToNode(context, targetNode);
        }

        trace.SetTag("handling_node", true);

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

            // Generate test coverage report
            if (context.Options?.GenerateCoverageReport == true)
            {
                try
                {
                    var coverageReport = await TestCoverageGenerator.GenerateReportAsync(context);
                    pipelineResult.CoverageReport = coverageReport;

                    // Generate HTML and JSON reports if requested
                    if (context.Options.GenerateHtmlCoverageReport)
                    {
                        var htmlReport = await TestCoverageGenerator.GenerateHtmlReportAsync(coverageReport);
                        // Store HTML report (implementation specific)
                    }

                    if (context.Options.GenerateJsonCoverageReport)
                    {
                        var jsonReport = await TestCoverageGenerator.GenerateJsonReportAsync(coverageReport);
                        // Store JSON report (implementation specific)
                    }

                    // Validate coverage thresholds
                    await TestCoverageGenerator.ValidateCoverageThresholdsAsync(coverageReport, pipelineResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate test coverage report");
                    pipelineResult.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = "Failed to generate test coverage report: " + ex.Message
                    });
                }
            }
            
            // Merge pipeline result with our tracking
            result.IsValid = pipelineResult.IsValid;
            result.Issues = pipelineResult.Issues;
            result.Statistics = pipelineResult.Statistics;
            
            result.State = result.FailedMiddleware.Any() 
                ? ValidationState.CompletedWithErrors 
                : ValidationState.Completed;
            
            result.IsPartialSuccess = result.FailedMiddleware.Any() && result.IsValid;
            
            // Cache the result before returning
            if (result.IsValid || result.IsPartialSuccess)
            {
                await _distributedCache.SetAsync(codeHash, context.Options, result);
                await _clusterCoordinator.BroadcastValidationResult(codeHash, result);
            }
            
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

    private string ComputeCodeHash(string code)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hashBytes);
    }

    private class ResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskIoMBPS { get; set; }
        public int ActiveThreads { get; set; }
    }

    private string GenerateNodeId()
    {
        return $"{Environment.MachineName}-{Guid.NewGuid()}";
    }

    private void InitializeClusterNode()
    {
        // Register with cluster coordinator
        _clusterCoordinator.RegisterNode(_nodeId, new NodeCapabilities
        {
            MaxConcurrentValidations = _config.MaxConcurrentValidations,
            SupportedLanguages = GetSupportedLanguages(),
            ResourceCapacity = new ResourceCapacity
            {
                CpuCores = Environment.ProcessorCount,
                TotalMemoryMB = GetTotalMemory(),
                DiskSpaceGB = GetAvailableDiskSpace()
            }
        });

        // Start leader election process
        _leaderElectionTimer = new Timer(async _ =>
        {
            try
            {
                _isLeader = await _clusterCoordinator.ParticipateInLeaderElection(_nodeId);
                if (_isLeader)
                {
                    await PerformLeaderDuties();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Leader election failed");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        // Start health monitoring
        StartResourceMonitoring();
    }

    private async Task<bool> ShouldHandleValidation(ValidationContext context)
    {
        // Check if we're the designated node for this validation
        var assignment = await _loadBalancer.GetAssignmentAsync(context);
        if (assignment.AssignedNodeId == _nodeId)
        {
            return true;
        }

        // If the assigned node is down, we might need to handle it
        if (!await _healthMonitor.IsNodeHealthy(assignment.AssignedNodeId))
        {
            await _loadBalancer.RebalanceWorkload();
            assignment = await _loadBalancer.GetAssignmentAsync(context);
            return assignment.AssignedNodeId == _nodeId;
        }

        return false;
    }

    private async Task<ValidationResult> ForwardValidationToNode(ValidationContext context, NodeAssignment targetNode)
    {
        try
        {
            return await _clusterCoordinator.ForwardValidationRequest(targetNode.NodeId, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward validation to node {NodeId}", targetNode.NodeId);
            
            // If forwarding fails, handle locally as fallback
            _logger.LogInformation("Handling validation locally as fallback");
            return await ProcessValidationLocally(context);
        }
    }

    private async Task PerformLeaderDuties()
    {
        // Coordinate cluster-wide operations
        await Task.WhenAll(
            _loadBalancer.RebalanceWorkload(),
            _healthMonitor.PerformClusterHealthCheck(),
            _clusterCoordinator.UpdateClusterState(),
            _distributedCache.OptimizeDistribution()
        );
    }

    private async Task ProcessValidationLocally(ValidationContext context)
    {
        // Implementation of local validation processing
        var result = new ValidationResult
        {
            State = ValidationState.InProgress,
            Language = context.Language
        };

        try
        {
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

            // Execute the validation pipeline
            ValidationDelegate pipeline = BuildPipeline(context);
            return await pipeline(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local validation processing failed");
            result.State = ValidationState.Failed;
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Local validation processing failed: " + ex.Message
            });
            return result;
        }
        finally
        {
            ReleaseResources(context);
        }
    }

    private string[] GetSupportedLanguages()
    {
        // Return list of languages this node can validate
        return new[] { "CSharp", "JavaScript", "Python" };
    }

    private long GetTotalMemory()
    {
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024); // Convert to MB
    }

    private long GetAvailableDiskSpace()
    {
        var drive = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory);
        return drive.AvailableFreeSpace / (1024 * 1024 * 1024); // Convert to GB
    }

    public void Dispose()
    {
        _leaderElectionTimer?.Dispose();
        _validationThrottle?.Dispose();
        _clusterCoordinator.UnregisterNode(_nodeId);
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
        var metrics = new ResourceMetrics
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsageMB = GetMemoryUsage(),
            DiskIoMBPS = GetDiskIoRate(),
            ActiveThreads = GetActiveThreadCount()
        };
        _metricsAggregator.RecordResourceUtilization(metrics);

        // Send metrics to alert manager for analysis
        await _alertManager.ProcessMetricsAsync(new Dictionary<ResourceMetricType, double>
        {
            [ResourceMetricType.CPU] = metrics.CpuUsagePercent,
            [ResourceMetricType.Memory] = metrics.MemoryUsageMB,
            [ResourceMetricType.DiskIO] = metrics.DiskIoMBPS
        }, "ValidationPipeline");

        // Store metrics in analytics system
        await _resourceAnalytics.StoreResourceUsageDataAsync(new ResourceUsageData
        {
            PipelineId = "ValidationPipeline",
            ValidatorType = "System",
            CpuUsagePercentage = metrics.CpuUsagePercent,
            MemoryUsageMB = metrics.MemoryUsageMB,
            DiskIOBytesPerSecond = metrics.DiskIoMBPS * 1024 * 1024, // Convert from MB/s to B/s
            Timestamp = DateTime.UtcNow
        });

        // Analyze for potential bottlenecks
        var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync();
        foreach (var bottleneck in bottlenecks)
        {
            await _alertManager.RaiseResourceAlert(new ResourceAlert
            {
                ResourceType = bottleneck.ResourceType,
                Severity = AlertSeverity.Warning,
                Message = $"Potential bottleneck detected: {bottleneck.Impact}",
                RecommendedAction = bottleneck.RecommendedAction
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
}