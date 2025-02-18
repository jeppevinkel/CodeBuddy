using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class ValidationOptions
{
    // General Validation Options
    public bool EnablePerformanceValidation { get; set; }
    public bool EnableMemoryValidation { get; set; }
    public bool EnableConcurrencyValidation { get; set; }
    public bool EnableResourceValidation { get; set; }
    public bool EnableSecurityValidation { get; set; }
    
    // Test Coverage Options
    public bool GenerateCoverageReport { get; set; }
    public bool GenerateHtmlCoverageReport { get; set; }
    public bool GenerateJsonCoverageReport { get; set; }
    public double MinimumCoverageThreshold { get; set; } = 80.0;
    public Dictionary<string, double> ModuleCoverageThresholds { get; set; } = new();
    public List<string> ExcludedCoveragePatterns { get; set; } = new();
    public bool TrackHistoricalCoverage { get; set; }
    
    // Validation Behavior Options
    public bool ContinueOnError { get; set; }
    public bool EnableParallelValidation { get; set; }
    public int MaxConcurrentValidations { get; set; } = 4;
    public bool EnableCache { get; set; }
    public int CacheExpirationMinutes { get; set; } = 60;
    
    // Resource Management Options
    public bool EnableResourceThrottling { get; set; }
    public double MaxCpuUsagePercent { get; set; } = 80;
    public double MaxMemoryUsageMB { get; set; } = 1024;
    public bool EnableAdaptiveThrottling { get; set; }
    
    // Monitoring Options
    public bool EnableTelemetry { get; set; }
    public bool EnablePerformanceMonitoring { get; set; }
    public bool EnableResourceMonitoring { get; set; }
    
    // Reporting Options
    public bool GenerateDetailedReport { get; set; }
    public bool IncludeStackTrace { get; set; }
    public bool IncludePerformanceMetrics { get; set; }
    public bool IncludeResourceMetrics { get; set; }
}