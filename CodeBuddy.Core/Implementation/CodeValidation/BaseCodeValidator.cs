using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
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
public abstract class BaseCodeValidator : ICodeValidator, IDisposable, IAsyncDisposable
{
    protected readonly ILogger _logger;
    private readonly Stopwatch _totalStopwatch = new();
    private readonly Dictionary<string, Stopwatch> _phaseStopwatches = new();
    private readonly PerformanceMonitor _performanceMonitor = new();
    private readonly List<string> _temporaryFiles = new();
    private readonly ResourceUsageTracker _resourceTracker;
    private readonly ObjectPool<byte[]> _bufferPool;
    private readonly ConcurrentQueue<IDisposable> _disposables = new();
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private bool _disposed;
    private FileOperationProgress _progress;
    
    // Resource management thresholds
    private const long MemoryThresholdBytes = 500 * 1024 * 1024; // 500MB
    private const long CriticalMemoryThresholdBytes = 800 * 1024 * 1024; // 800MB
    private const int MaxTemporaryFiles = 100;
    private const int BatchSize = 1024 * 1024; // 1MB batch size for large files

    protected BaseCodeValidator(ILogger logger)
    {
        _logger = logger;
        _resourceTracker = new ResourceUsageTracker();
        _bufferPool = ObjectPool.Create<byte[]>();
        _progress = new FileOperationProgress();
        
        // Register memory pressure listener
        GC.AddMemoryPressureListener(OnMemoryPressure);
    }

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { Language = language };
        _totalStopwatch.Restart();
        _performanceMonitor.Start();

        try
        {
            ThrowIfDisposed();
            
            // Monitor resource usage
            await MonitorResourcesAsync(cancellationToken);
            
            // Process large files in batches
            if (code.Length > BatchSize)
            {
                return await ProcessLargeFileAsync(code, language, options, cancellationToken);
            }

            // Update progress
            _progress.TotalOperations = GetTotalOperations(options);
            _progress.CurrentOperation = 0;

            if (options.ValidateSyntax)
            {
                await MeasurePhaseAsync("Syntax", async () => 
                {
                    await ValidateSyntaxAsync(code, result);
                    _progress.CurrentOperation++;
                    NotifyProgressChanged();
                }, cancellationToken);
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
    /// Performs gradual cleanup of resources when approaching thresholds
    /// </summary>
    private async Task GradualCleanupAsync()
    {
        await _cleanupLock.WaitAsync();
        try
        {
            // Release unused buffers
            _bufferPool.Clear();

            // Cleanup older temporary files
            if (_temporaryFiles.Count > MaxTemporaryFiles / 2)
            {
                await CleanupOldestTemporaryFilesAsync(MaxTemporaryFiles / 4);
            }

            // Suggest garbage collection
            GC.Collect(0, GCCollectionMode.Optimized, false);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Verifies that all resources have been properly cleaned up
    /// </summary>
    private async Task VerifyCleanupAsync()
    {
        var issues = new List<string>();

        // Check for leaked disposables
        if (!_disposables.IsEmpty)
        {
            issues.Add($"Found {_disposables.Count} undisposed resources");
        }

        // Check for remaining temporary files
        if (_temporaryFiles.Count > 0)
        {
            issues.Add($"Found {_temporaryFiles.Count} uncleaned temporary files");
        }

        // Log verification results
        if (issues.Count > 0)
        {
            _logger.LogError("Cleanup verification failed:\n{Issues}", string.Join("\n", issues));
            // Attempt recovery
            await EmergencyCleanupAsync();
        }
        else
        {
            _logger.LogInformation("Cleanup verification passed - all resources properly released");
        }
    }

    private async Task CleanupTemporaryFilesAsync()
    {
        foreach (var file in _temporaryFiles.ToList())
        {
            try
            {
                if (System.IO.File.Exists(file))
                {
                    await Task.Run(() => System.IO.File.Delete(file));
                }
                _temporaryFiles.Remove(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up temporary file: {File}", file);
            }
        }
    }

    private async Task CleanupOldestTemporaryFilesAsync(int count)
    {
        var filesToRemove = _temporaryFiles.Take(count).ToList();
        foreach (var file in filesToRemove)
        {
            try
            {
                if (System.IO.File.Exists(file))
                {
                    await Task.Run(() => System.IO.File.Delete(file));
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
    /// Checks if resource quotas are exceeded and takes corrective action.
    /// </summary>
    private async Task CheckResourceQuotas()
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
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await DisposeAsyncCore().ConfigureAwait(false);
        
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _cleanupLock.WaitAsync().ConfigureAwait(false);
            
            // Dispose all tracked disposables
            while (_disposables.TryDequeue(out var disposable))
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    disposable.Dispose();
            }

            // Clean up temporary files
            await CleanupTemporaryFilesAsync().ConfigureAwait(false);
            
            // Release memory pressure listener
            GC.RemoveMemoryPressureListener(OnMemoryPressure);
            
            // Wait for any ongoing operations to complete
            await Task.Delay(100).ConfigureAwait(false);
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private void OnMemoryPressure(GCMemoryPressureSource source, GCMemoryPressureLevel pressureLevel)
    {
        if (pressureLevel >= GCMemoryPressureLevel.Medium)
        {
            Task.Run(EmergencyCleanupAsync).ConfigureAwait(false);
        }
    }

    private async Task MonitorResourcesAsync(CancellationToken cancellationToken)
    {
        var currentUsage = await _resourceTracker.GetCurrentMemoryUsageAsync();
        
        if (currentUsage > CriticalMemoryThresholdBytes)
        {
            await EmergencyCleanupAsync();
        }
        else if (currentUsage > MemoryThresholdBytes)
        {
            await GradualCleanupAsync();
        }

        // Log resource usage for monitoring
        _logger.LogInformation(
            "Resource Usage - Memory: {MemoryMB}MB, Handles: {Handles}, Files: {Files}",
            currentUsage / (1024 * 1024),
            _resourceTracker.HandleCount,
            _temporaryFiles.Count);
    }

    private async Task ProcessLargeFileAsync(string code, string language, ValidationOptions options, CancellationToken cancellationToken)
    {
        var result = new ValidationResult { Language = language };
        var batches = (code.Length + BatchSize - 1) / BatchSize;
        _progress.TotalOperations = batches;

        for (int i = 0; i < batches && !cancellationToken.IsCancellationRequested; i++)
        {
            var start = i * BatchSize;
            var length = Math.Min(BatchSize, code.Length - start);
            var batch = code.Substring(start, length);

            var batchResult = await ProcessBatchAsync(batch, options, cancellationToken);
            result.Issues.AddRange(batchResult.Issues);
            
            _progress.CurrentOperation = i + 1;
            NotifyProgressChanged();
            
            // Check resources after each batch
            await MonitorResourcesAsync(cancellationToken);
        }

        return result;
    }

    private async Task<ValidationResult> ProcessBatchAsync(string batch, ValidationOptions options, CancellationToken cancellationToken)
    {
        // Get buffer from pool
        var buffer = _bufferPool.Get();
        try
        {
            return await ValidateWithResourceTrackingAsync(batch, options, buffer, cancellationToken);
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    private void NotifyProgressChanged()
    {
        _progress.ProgressPercentage = (int)((_progress.CurrentOperation / (double)_progress.TotalOperations) * 100);
        _logger.LogInformation("Validation Progress: {Progress}%", _progress.ProgressPercentage);
    }

    private int GetTotalOperations(ValidationOptions options)
    {
        int total = 0;
        if (options.ValidateSyntax) total++;
        if (options.ValidateSecurity) total++;
        if (options.ValidateStyle) total++;
        if (options.ValidateBestPractices) total++;
        if (options.ValidateErrorHandling) total++;
        if (options.CustomRules?.Count > 0) total++;
        return total;
    }

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