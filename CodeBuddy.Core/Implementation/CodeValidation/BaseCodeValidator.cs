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

            // Parallel validation for independent phases
            var parallelTasks = new List<Task>();
            var validationPhases = new List<(string Name, Func<Task> Task)>();

            if (options.ValidateSecurity)
                validationPhases.Add(("Security", () => ValidateSecurityAsync(code, result)));
            
            if (options.ValidateStyle)
                validationPhases.Add(("Style", () => ValidateStyleAsync(code, result)));
            
            if (options.ValidateBestPractices)
                validationPhases.Add(("BestPractices", () => ValidateBestPracticesAsync(code, result)));
            
            if (options.ValidateErrorHandling)
                validationPhases.Add(("ErrorHandling", () => ValidateErrorHandlingAsync(code, result)));

            // Start parallel validation phases
            foreach (var phase in validationPhases)
            {
                parallelTasks.Add(MeasureParallelPhaseAsync(phase.Name, phase.Task));
            }

            // Wait for all parallel validations to complete
            await Task.WhenAll(parallelTasks);

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