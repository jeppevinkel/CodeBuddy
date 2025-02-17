using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class ValidationOptions
{
    public bool ValidateSyntax { get; set; } = true;
    public bool ValidateSecurity { get; set; } = true;
    public bool ValidateStyle { get; set; } = true;
    public bool ValidateBestPractices { get; set; } = true;
    public bool ValidateErrorHandling { get; set; } = true;
    public Dictionary<string, object> CustomRules { get; set; } = new();
    public PerformanceThresholds PerformanceThresholds { get; set; } = new();
}

public class PerformanceThresholds
{
    public double MaxValidationTimeMs { get; set; } = 5000; // 5 seconds
    public long MaxMemoryUsageBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB
    public double MaxCpuUtilizationPercent { get; set; } = 80;
    public Dictionary<string, double> PhaseTimeThresholdsMs { get; set; } = new();
}