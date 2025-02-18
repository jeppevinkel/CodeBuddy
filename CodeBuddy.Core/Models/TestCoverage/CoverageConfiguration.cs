using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeBuddy.Core.Models.TestCoverage;

public class CoverageConfiguration
{
    public double OverallThreshold { get; set; } = 80.0;
    public Dictionary<string, double> ModuleThresholds { get; set; } = new();
    public Dictionary<string, CoverageTypeRequirements> CodeTypeThresholds { get; set; } = new();
    public List<string> ExclusionPatterns { get; set; } = new();
    public ValidationRules ValidationRules { get; set; } = new();
    
    public double GetThresholdForModule(string moduleName)
    {
        if (ModuleThresholds.TryGetValue(moduleName, out var threshold))
            return threshold;
        return OverallThreshold;
    }

    public bool ShouldExclude(string filePath)
    {
        foreach (var pattern in ExclusionPatterns)
        {
            if (Regex.IsMatch(filePath, pattern))
                return true;
        }
        return false;
    }

    public CoverageTypeRequirements GetRequirementsForCodeType(string codeType)
    {
        if (CodeTypeThresholds.TryGetValue(codeType, out var requirements))
            return requirements;
        return new CoverageTypeRequirements();
    }
}

public class CoverageTypeRequirements
{
    public double MinimumOverallCoverage { get; set; } = 80.0;
    public double MinimumBranchCoverage { get; set; } = 70.0;
    public double MinimumStatementCoverage { get; set; } = 80.0;
    public bool RequirePublicApiCoverage { get; set; } = true;
    public bool RequireIntegrationPointsCoverage { get; set; } = true;
}

public class ValidationRules
{
    public bool EnforceCriticalPathCoverage { get; set; } = true;
    public double CriticalPathThreshold { get; set; } = 90.0;
    public bool PreventCoverageDecrease { get; set; } = true;
    public bool RequireNewCodeCoverage { get; set; } = true;
    public double NewCodeCoverageThreshold { get; set; } = 85.0;
    public bool ValidatePublicApiCoverage { get; set; } = true;
    public double PublicApiCoverageThreshold { get; set; } = 90.0;
}