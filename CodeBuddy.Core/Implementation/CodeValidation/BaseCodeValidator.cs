using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public abstract class BaseCodeValidator : ICodeValidator
{
    protected readonly ILogger _logger;

    protected BaseCodeValidator(ILogger logger)
    {
        _logger = logger;
    }

    private readonly Stopwatch _totalStopwatch = new();
    private readonly Dictionary<string, Stopwatch> _phaseStopwatches = new();
    private readonly PerformanceMonitor _performanceMonitor = new();

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
    {
        var result = new ValidationResult { Language = language };
        _totalStopwatch.Restart();
        _performanceMonitor.Start();

        try
        {
            // Sequential validation for syntax as it's a prerequisite
            if (options.ValidateSyntax)
            {
                await MeasurePhaseAsync("Syntax", () => ValidateSyntaxAsync(code, result));
                if (result.Issues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    result.IsValid = false;
                    return result;
                }
            }

            // Get available system resources
            var (availableCores, memoryUsage, cpuUsage) = GetSystemResources();
            var canRunParallel = CanRunParallel(options.ParallelOptions, availableCores, memoryUsage, cpuUsage);

            var validationPhases = new List<(string Name, Func<Task> Task)>();

            if (options.ValidateSecurity)
                validationPhases.Add(("Security", () => ValidateSecurityAsync(code, result)));
            
            if (options.ValidateStyle)
                validationPhases.Add(("Style", () => ValidateStyleAsync(code, result)));
            
            if (options.ValidateBestPractices)
                validationPhases.Add(("BestPractices", () => ValidateBestPracticesAsync(code, result)));
            
            if (options.ValidateErrorHandling)
                validationPhases.Add(("ErrorHandling", () => ValidateErrorHandlingAsync(code, result)));

            if (canRunParallel)
            {
                await ExecuteInParallel(validationPhases, options, result);
            }
            else
            {
                foreach (var phase in validationPhases)
                {
                    await MeasurePhaseAsync(phase.Name, phase.Task);
                }
            }

            // Run custom rules last as they might depend on other validation results
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
        _performanceMonitor.Start(phaseName);
        await phase();
        stopwatch.Stop();
        _performanceMonitor.End(phaseName);
        _phaseStopwatches[phaseName] = stopwatch;
    }

    private async Task MeasureParallelPhaseAsync(string phaseName, Func<Task> phase)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _performanceMonitor.Start(phaseName);
        await Task.Run(phase);
        stopwatch.Stop();
        _performanceMonitor.End(phaseName);
        _phaseStopwatches[phaseName] = stopwatch;
    }

    private void CalculatePhaseOverlap(PerformanceMetrics metrics, ParallelExecutionReport report)
    {
        foreach (var phase in report.PhaseTimings)
        {
            var otherPhases = report.PhaseTimings
                .Where(x => x.Key != phase.Key && x.Value.WasParallel)
                .ToList();

            if (!otherPhases.Any()) continue;

            var phaseSpan = phase.Value.Duration;
            var overlapTime = otherPhases.Sum(other => 
            {
                var overlap = Math.Min(
                    phase.Value.Duration.TotalMilliseconds,
                    other.Value.Duration.TotalMilliseconds
                );
                return overlap;
            });

            metrics.PhaseOverlapPercentages[phase.Key] = 
                (overlapTime / phaseSpan.TotalMilliseconds) * 100;
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
        
        // Resource utilization and parallel execution metrics
        var resourceMetrics = _performanceMonitor.GetMetrics();
        metrics.PeakMemoryUsageBytes = resourceMetrics.PeakMemoryBytes;
        metrics.CpuUtilizationPercent = resourceMetrics.CpuPercent;
        metrics.ResourceUtilization["ThreadCount"] = resourceMetrics.ThreadCount;
        metrics.ResourceUtilization["HandleCount"] = resourceMetrics.HandleCount;
        
        // Parallel execution metrics
        metrics.ConcurrentOperationsCount = resourceMetrics.ConcurrentOps;
        metrics.ThreadPoolUtilizationPercent = resourceMetrics.ThreadPoolUtilization;
        metrics.ParallelEfficiencyPercent = _performanceMonitor.CalculateParallelEfficiency();
        
        // Calculate sequential vs parallel execution time
        var sequentialTime = metrics.PhaseTimings.Values.Sum(t => t.TotalMilliseconds);
        metrics.SequentialExecutionTime = TimeSpan.FromMilliseconds(sequentialTime);
        metrics.ParallelExecutionTime = _totalStopwatch.Elapsed;

        // Detect bottlenecks
        DetectBottlenecks(metrics);
    }

    private async Task ExecuteInParallel(List<(string Name, Func<Task> Task)> phases, ValidationOptions options, ValidationResult result)
    {
        var parallelTasks = new List<Task>();
        var maxConcurrent = DetermineMaxConcurrentTasks(options.ParallelOptions);
        var runningTasks = new Dictionary<string, Task>();

        foreach (var phase in phases)
        {
            if (options.ParallelOptions.SequentialPhases.Contains(phase.Name))
            {
                // Run sequential phases immediately
                await MeasurePhaseAsync(phase.Name, phase.Task);
                continue;
            }

            // Wait if we've reached the maximum concurrent tasks
            while (maxConcurrent > 0 && runningTasks.Count >= maxConcurrent)
            {
                var completedTask = await Task.WhenAny(runningTasks.Values);
                var completedPhase = runningTasks.First(x => x.Value == completedTask);
                runningTasks.Remove(completedPhase.Key);
            }

            var task = MeasureParallelPhaseAsync(phase.Name, phase.Task);
            runningTasks[phase.Name] = task;
            parallelTasks.Add(task);
        }

        // Wait for remaining tasks
        await Task.WhenAll(parallelTasks);
    }

    private (int Cores, double MemoryUsage, double CpuUsage) GetSystemResources()
    {
        var cores = Environment.ProcessorCount;
        var process = Process.GetCurrentProcess();
        var totalMemory = process.WorkingSet64;
        var systemMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        var memoryUsagePercent = (double)totalMemory / systemMemory * 100;
        
        var metrics = _performanceMonitor.GetMetrics();
        return (cores, memoryUsagePercent, metrics.CpuPercent);
    }

    private bool CanRunParallel(ParallelValidationOptions options, int availableCores, double memoryUsage, double cpuUsage)
    {
        if (!options.UseAdaptiveParallelization)
            return true;

        if (availableCores < options.MinimumCpuCores)
            return false;

        if (cpuUsage > options.MaxCpuUtilization)
            return false;

        if (memoryUsage > options.MaxMemoryUtilization)
            return false;

        return true;
    }

    private int DetermineMaxConcurrentTasks(ParallelValidationOptions options)
    {
        if (options.MaxConcurrentPhases > 0)
            return options.MaxConcurrentPhases;

        if (options.UseAdaptiveParallelization)
        {
            var (cores, memoryUsage, cpuUsage) = GetSystemResources();
            var availableCores = cores - 1; // Reserve one core for system operations
            return Math.Max(1, availableCores);
        }

        return 0; // Unlimited
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