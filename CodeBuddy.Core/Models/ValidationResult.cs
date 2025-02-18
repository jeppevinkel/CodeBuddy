using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public string Language { get; set; }
    public ValidationStatistics Statistics { get; set; } = new();
    
    // Error recovery and resilience information
    public ValidationState State { get; set; } = ValidationState.NotStarted;
    public bool IsPartialSuccess { get; set; }
    public List<MiddlewareFailure> FailedMiddleware { get; set; } = new();
    public List<string> SkippedMiddleware { get; set; } = new();
    public RecoveryMetrics RecoveryStats { get; set; } = new();
    
    // Test Coverage Data
    public TestCoverageReport CoverageReport { get; set; }
    public Dictionary<string, LineCoverage> LineCoverageData { get; set; } = new();
    public List<string> IgnoredCoverageRegions { get; set; } = new();
    public List<CoverageTrendPoint> HistoricalCoverage { get; set; } = new();
    public CoverageValidationResult CoverageValidation { get; set; } = new();
}

public class CoverageValidationResult
{
    public bool MeetsThreshold { get; set; }
    public double ThresholdPercentage { get; set; }
    public Dictionary<string, double> ModuleThresholds { get; set; } = new();
    public List<string> ModulesBelowThreshold { get; set; } = new();
    public List<CoverageRecommendation> ImprovementSuggestions { get; set; } = new();
}

public enum ValidationState
{
    NotStarted,
    InProgress,
    Completed,
    CompletedWithErrors,
    Failed,
    Recovered
}

public class MiddlewareFailure
{
    public string MiddlewareName { get; set; }
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
    public int FailureCount { get; set; }
    public int RetryAttempts { get; set; }
    public bool CircuitBreakerTripped { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
}

public class RecoveryMetrics
{
    public int TotalRecoveryAttempts { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public double AverageRecoveryTimeMs { get; set; }
    public List<FailurePattern> DetectedPatterns { get; set; } = new();
    public double PerformanceImpactMs { get; set; }
}

public class FailurePattern
{
    public string Pattern { get; set; }
    public int Occurrences { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
}

public class ValidationIssue
{
    public string Code { get; set; }
    public string Message { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Location { get; set; }
    public string Suggestion { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    SecurityVulnerability
}

public class ValidationStatistics
{
    public int TotalIssues { get; set; }
    public int SecurityIssues { get; set; }
    public int StyleIssues { get; set; }
    public int BestPracticeIssues { get; set; }
    public double CodeCoveragePercentage { get; set; }
    public int CyclomaticComplexity { get; set; }
    
    // Performance metrics
    public PerformanceMetrics Performance { get; set; } = new();
}