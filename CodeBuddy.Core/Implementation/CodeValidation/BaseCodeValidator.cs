using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

/// <summary>
/// Base class for code validators implementing resource management and cleanup mechanisms.
/// Implements IDisposable to ensure proper cleanup of resources.
/// </summary>
/// <remarks>
/// This class manages validation resources including:
/// - Performance monitoring and metrics collection
/// - Temporary files and buffers
/// - Memory pressure monitoring
/// - Resource usage quotas
/// 
/// Proper disposal is required to prevent resource leaks. Use in a using block:
/// <code>
/// using (var validator = new MyCodeValidator(logger))
/// {
///     var result = await validator.ValidateAsync(code, language, options);
///     private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BaseCodeValidator), 
                "Cannot use validator after disposal. Create a new instance.");
        }
    }

    /// <summary>
    /// Performs cleanup of managed and unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs cleanup of managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            _performanceMonitor?.Dispose();
            _resourceTracker?.Dispose();
            
            foreach (var stopwatch in _phaseStopwatches.Values)
            {
                stopwatch.Stop();
            }
            _phaseStopwatches.Clear();
            
            CleanupTemporaryFiles();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure cleanup of unmanaged resources if Dispose is not called.
    /// </summary>
    ~BaseCodeValidator()
    {
        Dispose(false);
    }
}
/// </code>
/// </remarks>
public abstract class BaseCodeValidator : ICodeValidator, IDisposable
{
    protected readonly ILogger _logger;
    private readonly Stopwatch _totalStopwatch = new();
    private readonly Dictionary<string, Stopwatch> _phaseStopwatches = new();
    private readonly PerformanceMonitor _performanceMonitor = new();
    private readonly List<string> _temporaryFiles = new();
    private readonly ResourceUsageTracker _resourceTracker;
    private bool _disposed;
    private const long MemoryThresholdBytes = 500 * 1024 * 1024; // 500MB
    private const int MaxTemporaryFiles = 100;

    protected BaseCodeValidator(ILogger logger)
    {
        _logger = logger;
        _resourceTracker = new ResourceUsageTracker();
    }

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
    {
        var result = new ValidationResult { Language = language };
        _totalStopwatch.Restart();
        _performanceMonitor.Start();

        try
        {
            ThrowIfDisposed();
            
            if (_resourceTracker.CurrentMemoryUsage > MemoryThresholdBytes)
            {
                await EmergencyCleanupAsync();
            }

            if (options.ValidateSyntax)
            {
                await MeasurePhaseAsync("Syntax", () => ValidateSyntaxAsync(code, result));
            }

            CheckResourceQuotas();

            if (options.ValidateSecurity)
            {
                await MeasurePhaseAsync("Security", () => ValidateSecurityAsync(code, result));
            }

            await _resourceTracker.MonitorResourcesAsync();

            if (options.ValidateStyle)
            {
                await MeasurePhaseAsync("Style", () => ValidateStyleAsync(code, result));
            }

            if (options.ValidateBestPractices)
            {
                await MeasurePhaseAsync("BestPractices", () => ValidateBestPracticesAsync(code, result));
            }

            if (options.ValidateErrorHandling)
            {
                await MeasurePhaseAsync("ErrorHandling", () => ValidateErrorHandlingAsync(code, result));
            }

            await MeasurePhaseAsync("CustomRules", () => ValidateCustomRulesAsync(code, result, options.CustomRules));

            // Calculate statistics and performance metrics
            CalculateStatistics(result);
            CollectPerformanceMetrics(result);

            // Set overall validation status
            result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error 
                || i.Severity == ValidationSeverity.SecurityVulnerability);

            // Check performance thresholds
            CheckPerformanceThresholds(result, options);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code validation for language {Language}", language);
            result.Issues.Add(new ValidationIssue
            {
                Code = "VAL001",
                Message = $"Validation process failed: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            result.IsValid = false;
            return result;
        }
    }

    protected abstract Task ValidateSyntaxAsync(string code, ValidationResult result);
    protected abstract Task ValidateSecurityAsync(string code, ValidationResult result);
    protected abstract Task ValidateStyleAsync(string code, ValidationResult result);
    protected abstract Task ValidateBestPracticesAsync(string code, ValidationResult result);
    protected abstract Task ValidateErrorHandlingAsync(string code, ValidationResult result);
    protected abstract Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules);

    private void CalculateStatistics(ValidationResult result)
    {
        result.Statistics.TotalIssues = result.Issues.Count;
        result.Statistics.SecurityIssues = result.Issues.Count(i => i.Severity == ValidationSeverity.SecurityVulnerability);
        result.Statistics.StyleIssues = result.Issues.Count(i => i.Code.StartsWith("STYLE"));
        result.Statistics.BestPracticeIssues = result.Issues.Count(i => i.Code.StartsWith("BP"));
    }

    private async Task MeasurePhaseAsync(string phaseName, Func<Task> phase)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        await phase();
        stopwatch.Stop();
        _phaseStopwatches[phaseName] = stopwatch;
    }

    /// <summary>
    /// Monitors and manages resource usage during validation.
    /// </summary>
    private class ResourceUsageTracker : IDisposable
    {
        private Timer _monitoringTimer;
        private long _currentMemoryUsage;
        private int _handleCount;
        private bool _disposed;

        public long CurrentMemoryUsage => _currentMemoryUsage;
        public int HandleCount => _handleCount;

        public ResourceUsageTracker()
        {
            _monitoringTimer = new Timer(MonitorResources, null, 1000, 1000);
        }

        public async Task MonitorResourcesAsync()
        {
            var process = Process.GetCurrentProcess();
            _currentMemoryUsage = process.WorkingSet64;
            _handleCount = process.HandleCount;

            if (_currentMemoryUsage > MemoryThresholdBytes)
            {
                GC.Collect();
                await Task.Delay(100); // Allow GC to complete
            }
        }

        private void MonitorResources(object state)
        {
            if (_disposed) return;
            MonitorResourcesAsync().Wait();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();
                _monitoringTimer = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Checks if resource quotas are exceeded and takes corrective action.
    /// </summary>
    private void CheckResourceQuotas()
    {
        if (_temporaryFiles.Count >= MaxTemporaryFiles)
        {
            _logger.LogWarning("Temporary file quota exceeded. Cleaning up oldest files.");
            CleanupOldestTemporaryFiles(MaxTemporaryFiles / 2);
        }

        if (_resourceTracker.HandleCount > 1000)
        {
            _logger.LogWarning("Handle count exceeds threshold. Initiating cleanup.");
            GC.Collect();
        }
    }

    /// <summary>
    /// Performs emergency cleanup when resources are critically low.
    /// </summary>
    private async Task EmergencyCleanupAsync()
    {
        _logger.LogWarning("Initiating emergency cleanup due to high memory usage");
        
        // Clear all caches and temporary storage
        _phaseStopwatches.Clear();
        CleanupTemporaryFiles();
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        // Allow system to stabilize
        await Task.Delay(100);
    }

    /// <summary>
    /// Cleans up the oldest temporary files to free resources.
    /// </summary>
    private void CleanupOldestTemporaryFiles(int count)
    {
        var filesToRemove = _temporaryFiles.Take(count).ToList();
        foreach (var file in filesToRemove)
        {
            try
            {
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                }
                _temporaryFiles.Remove(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up temporary file: {File}", file);
            }
        }
    }

    /// <summary>
    /// Cleans up all temporary files.
    /// </summary>
    private void CleanupTemporaryFiles()
    {
        foreach (var file in _temporaryFiles.ToList())
        {
            try
            {
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                }
                _temporaryFiles.Remove(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up temporary file: {File}", file);
            }
        }
    }

    private void CollectPerformanceMetrics(ValidationResult result)
    {
        _totalStopwatch.Stop();
        var metrics = result.Statistics.Performance;

        // Phase timings
        foreach (var (phase, stopwatch) in _phaseStopwatches)
        {
            metrics.PhaseTimings[phase] = stopwatch.Elapsed;
        }

        // Overall metrics
        metrics.AverageValidationTimeMs = _totalStopwatch.Elapsed.TotalMilliseconds;
        
        // Resource utilization
        var resourceMetrics = _performanceMonitor.GetMetrics();
        metrics.PeakMemoryUsageBytes = resourceMetrics.PeakMemoryBytes;
        metrics.CpuUtilizationPercent = resourceMetrics.CpuPercent;
        metrics.ResourceUtilization["ThreadCount"] = resourceMetrics.ThreadCount;
        metrics.ResourceUtilization["HandleCount"] = resourceMetrics.HandleCount;

        // Detect bottlenecks
        DetectBottlenecks(metrics);
    }

    private void DetectBottlenecks(PerformanceMetrics metrics)
    {
        // Identify phases that took more than 25% of total time
        var totalTime = metrics.PhaseTimings.Values.Sum(t => t.TotalMilliseconds);
        foreach (var (phase, timing) in metrics.PhaseTimings)
        {
            var percentage = (timing.TotalMilliseconds / totalTime) * 100;
            if (percentage > 25)
            {
                metrics.Bottlenecks.Add(new PerformanceBottleneck
                {
                    Phase = phase,
                    Description = $"Phase takes {percentage:F1}% of total validation time",
                    ImpactScore = percentage,
                    Recommendation = "Consider optimizing this phase or running it in parallel if possible"
                });
            }
        }

        // Check memory usage
        if (metrics.PeakMemoryUsageBytes > 500 * 1024 * 1024) // 500MB
        {
            metrics.Bottlenecks.Add(new PerformanceBottleneck
            {
                Phase = "Memory",
                Description = "High memory usage detected",
                ImpactScore = 80,
                Recommendation = "Review memory allocation patterns and consider implementing memory pooling"
            });
        }
    }

    private void CheckPerformanceThresholds(ValidationResult result, ValidationOptions options)
    {
        if (options.PerformanceThresholds == null) return;

        var metrics = result.Statistics.Performance;
        var thresholds = options.PerformanceThresholds;

        if (metrics.AverageValidationTimeMs > thresholds.MaxValidationTimeMs)
        {
            _logger.LogWarning("Validation exceeded time threshold: {ActualMs}ms > {ThresholdMs}ms",
                metrics.AverageValidationTimeMs, thresholds.MaxValidationTimeMs);
        }

        if (metrics.PeakMemoryUsageBytes > thresholds.MaxMemoryUsageBytes)
        {
            _logger.LogWarning("Validation exceeded memory threshold: {ActualMB}MB > {ThresholdMB}MB",
                metrics.PeakMemoryUsageBytes / (1024 * 1024), thresholds.MaxMemoryUsageBytes / (1024 * 1024));
        }
    }
}