using System.Collections.Generic;

namespace CodeBuddy.Core.Models.TestCoverage;

public class CoverageValidationResult
{
    public double ThresholdPercentage { get; set; }
    public bool MeetsThreshold { get; set; }
    public Dictionary<string, double> ModuleThresholds { get; set; } = new();
    public List<string> ModulesBelowThreshold { get; set; } = new();
    public List<CoverageRecommendation> ImprovementSuggestions { get; set; } = new();
    
    // Enhanced validation results
    public Dictionary<string, bool> DetailedValidation { get; set; } = new();
    public Dictionary<string, bool> LegacyCodeValidation { get; set; } = new();
    
    // Threshold violation details
    public List<ThresholdViolation> Violations { get; set; } = new();
    
    // Historical trend analysis
    public List<TrendAnalysis> CoverageTrends { get; set; } = new();
}

public class ThresholdViolation
{
    public string ModuleName { get; set; }
    public double CurrentCoverage { get; set; }
    public double RequiredCoverage { get; set; }
    public string ViolationType { get; set; }
    public string Severity { get; set; }
    public List<string> AffectedFiles { get; set; } = new();
    public string RecommendedAction { get; set; }
}

public class TrendAnalysis
{
    public string Period { get; set; }
    public double StartCoverage { get; set; }
    public double EndCoverage { get; set; }
    public double CoverageChange { get; set; }
    public List<string> SignificantChanges { get; set; } = new();
    public bool MeetsTrendTarget { get; set; }
}