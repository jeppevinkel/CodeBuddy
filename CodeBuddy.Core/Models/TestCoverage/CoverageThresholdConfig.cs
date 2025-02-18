using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeBuddy.Core.Models.TestCoverage;

public class CoverageThresholdConfig
{
    // Overall project thresholds
    public double MinimumOverallCoverage { get; set; } = 80.0;
    public double MinimumBranchCoverage { get; set; } = 75.0;
    public double MinimumStatementCoverage { get; set; } = 80.0;

    // Per-module configuration
    public Dictionary<string, ModuleThresholdConfig> ModuleThresholds { get; set; } = new();
    
    // Legacy code configurations
    public LegacyCodeConfig LegacyCodeSettings { get; set; } = new();
    
    // Exclusion patterns
    public ExclusionConfig Exclusions { get; set; } = new();
    
    // Reporting configuration
    public ReportingConfig ReportingSettings { get; set; } = new();
}

public class ModuleThresholdConfig
{
    public string ModuleName { get; set; }
    public double MinimumCoverage { get; set; }
    public double MinimumBranchCoverage { get; set; }
    public double MinimumStatementCoverage { get; set; }
    public bool IsCritical { get; set; }
    public GradualIncreaseConfig GradualIncrease { get; set; }
}

public class LegacyCodeConfig
{
    public bool EnableLegacyCodeDifferentiation { get; set; } = true;
    public double LegacyCodeMinimumCoverage { get; set; } = 60.0;
    public double NewCodeMinimumCoverage { get; set; } = 85.0;
    public int LegacyCodeAgeThresholdDays { get; set; } = 365;
}

public class ExclusionConfig
{
    public List<string> ExcludedPaths { get; set; } = new();
    public List<Regex> ExcludedPatterns { get; set; } = new();
    public List<string> ExcludedFileTypes { get; set; } = new();
    public Dictionary<string, string> ExclusionReasons { get; set; } = new();
}

public class ReportingConfig
{
    public List<string> EnabledReportFormats { get; set; } = new() { "HTML", "JSON" };
    public string CustomHtmlTemplatePath { get; set; }
    public bool IncludeHistoricalTrends { get; set; } = true;
    public bool GenerateRecommendations { get; set; } = true;
    public CiCdIntegrationConfig CiCdIntegration { get; set; } = new();
}

public class CiCdIntegrationConfig
{
    public bool EnableCiCdIntegration { get; set; } = true;
    public string PipelineStatusVariable { get; set; } = "COVERAGE_STATUS";
    public bool FailBuildOnThresholdViolation { get; set; } = true;
    public Dictionary<string, string> CustomVariables { get; set; } = new();
}

public class GradualIncreaseConfig
{
    public bool EnableGradualIncrease { get; set; } = false;
    public double StartingThreshold { get; set; }
    public double TargetThreshold { get; set; }
    public int IncrementPercentage { get; set; }
    public int IncrementIntervalDays { get; set; }
}