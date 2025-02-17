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
    public ParallelValidationOptions ParallelOptions { get; set; } = new();
}

public class ParallelValidationOptions
{
    // Maximum number of concurrent validation phases (0 = unlimited)
    public int MaxConcurrentPhases { get; set; } = 0;
    
    // Whether to use adaptive parallelization based on system resources
    public bool UseAdaptiveParallelization { get; set; } = true;
    
    // Minimum CPU cores required for parallel execution
    public int MinimumCpuCores { get; set; } = 2;
    
    // Maximum CPU utilization percentage before reducing parallelization
    public double MaxCpuUtilization { get; set; } = 80;
    
    // Maximum memory utilization percentage before reducing parallelization
    public double MaxMemoryUtilization { get; set; } = 75;
    
    // Phases that must run sequentially (if any)
    public HashSet<string> SequentialPhases { get; set; } = new() { "Syntax" };
}

public class PerformanceThresholds
{
    public double MaxValidationTimeMs { get; set; } = 5000; // 5 seconds
    public long MaxMemoryUsageBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB
    public double MaxCpuUtilizationPercent { get; set; } = 80;
    public Dictionary<string, double> PhaseTimeThresholdsMs { get; set; } = new();
}