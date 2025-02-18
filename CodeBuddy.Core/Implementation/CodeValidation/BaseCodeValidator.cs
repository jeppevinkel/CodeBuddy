using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Security;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public abstract class BaseCodeValidator : ICodeValidator
{
    protected readonly ILogger _logger;
    protected readonly ISecurityScanner _securityScanner;

    protected BaseCodeValidator(ILogger logger, ISecurityScanner securityScanner)
    {
        _logger = logger;
        _securityScanner = securityScanner;
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
            if (options.ValidateSyntax)
            {
                await MeasurePhaseAsync("Syntax", () => ValidateSyntaxAsync(code, result));
            }

            if (options.ValidateSecurity)
            {
                await MeasurePhaseAsync("Security", () => ValidateSecurityAsync(code, result));
            }

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
    protected virtual async Task ValidateSecurityAsync(string code, ValidationResult result)
    {
        try
        {
            var scanOptions = new SecurityScanOptions
            {
                SeverityThreshold = 1, // We'll filter by severity in the result processing
                IncludeRuleDescriptions = true,
                ScanDependencies = true,
                ScanLevel = SecurityScanLevel.Thorough
            };

            var scanResult = await _securityScanner.ScanAsync(code, scanOptions);

            // Convert security vulnerabilities to validation issues
            foreach (var vulnerability in scanResult.Vulnerabilities)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Code = vulnerability.Id,
                    Message = $"{vulnerability.Title}: {vulnerability.Description}",
                    Location = vulnerability.AffectedCodeLocation,
                    Severity = ConvertSeverityToValidationSeverity(vulnerability.Severity),
                    Category = "Security",
                    Type = vulnerability.VulnerabilityType,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        ["CWE"] = vulnerability.CWE,
                        ["OWASP"] = vulnerability.OWASP,
                        ["Remediation"] = vulnerability.RemediationGuidance
                    }
                });
            }

            // Add security scan statistics
            result.Statistics.SecurityVulnerabilities = scanResult.Vulnerabilities.Count;
            foreach (var stat in scanResult.VulnerabilityStatistics)
            {
                result.Statistics.SecurityBreakdown[stat.Key] = stat.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during security validation");
            result.Issues.Add(new ValidationIssue
            {
                Code = "SEC001",
                Message = $"Security validation failed: {ex.Message}",
                Severity = ValidationSeverity.Error,
                Category = "Security"
            });
        }
    }

    private ValidationSeverity ConvertSeverityToValidationSeverity(int securitySeverity)
    {
        return securitySeverity switch
        {
            >= 9 => ValidationSeverity.SecurityVulnerability,
            >= 7 => ValidationSeverity.Error,
            >= 4 => ValidationSeverity.Warning,
            _ => ValidationSeverity.Information
        };
    }
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