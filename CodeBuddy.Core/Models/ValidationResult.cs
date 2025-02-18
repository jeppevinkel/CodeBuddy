using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Messages { get; set; } = new();
    public CoverageValidationResult CoverageValidation { get; set; }
    
    // Detailed validation results
    public bool IsCoverageValid => CoverageValidation?.MeetsThreshold ?? false;
    public bool AreCriticalModulesValid => !CoverageValidation?.ModulesBelowThreshold
        .Any(module => CoverageValidation.ModuleThresholds.ContainsKey(module)) ?? false;
    
    public ValidationSummary Summary => new()
    {
        OverallResult = IsValid,
        CoverageStatus = IsCoverageValid,
        CriticalModulesStatus = AreCriticalModulesValid,
        Messages = Messages,
        CoverageDetails = CoverageValidation
    };
}

public class ValidationSummary
{
    public bool OverallResult { get; set; }
    public bool CoverageStatus { get; set; }
    public bool CriticalModulesStatus { get; set; }
    public List<string> Messages { get; set; }
    public CoverageValidationResult CoverageDetails { get; set; }
}