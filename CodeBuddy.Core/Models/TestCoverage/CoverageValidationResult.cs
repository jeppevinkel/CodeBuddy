using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.TestCoverage;

public class CoverageValidationResult
{
    public bool MeetsOverallThreshold { get; set; }
    public double ActualOverallCoverage { get; set; }
    public double RequiredOverallThreshold { get; set; }
    
    public List<ModuleValidationResult> ModuleResults { get; set; } = new();
    public List<CriticalPathValidation> CriticalPathResults { get; set; } = new();
    public List<ApiCoverageValidation> ApiCoverageResults { get; set; } = new();
    
    public List<CoverageViolation> Violations { get; set; } = new();
    public List<CoverageRecommendation> Recommendations { get; set; } = new();
    
    public CoverageTrendAnalysis TrendAnalysis { get; set; }
    public RiskAssessment RiskAssessment { get; set; }
}

public class ModuleValidationResult
{
    public string ModuleName { get; set; }
    public double ActualCoverage { get; set; }
    public double RequiredThreshold { get; set; }
    public bool MeetsThreshold { get; set; }
    public Dictionary<string, double> ComponentCoverage { get; set; } = new();
    public List<string> UncoveredCriticalSections { get; set; } = new();
}

public class CriticalPathValidation
{
    public string PathName { get; set; }
    public double Coverage { get; set; }
    public double RequiredThreshold { get; set; }
    public bool MeetsThreshold { get; set; }
    public string Impact { get; set; }
    public string Risk { get; set; }
}

public class ApiCoverageValidation
{
    public string ApiName { get; set; }
    public double Coverage { get; set; }
    public double RequiredThreshold { get; set; }
    public bool MeetsThreshold { get; set; }
    public List<string> UncoveredEndpoints { get; set; } = new();
}

public class CoverageViolation
{
    public string Type { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
    public string Location { get; set; }
    public double RequiredCoverage { get; set; }
    public double ActualCoverage { get; set; }
    public string Impact { get; set; }
}

public class CoverageTrendAnalysis
{
    public double CoverageChange { get; set; }
    public List<CoverageTrendPoint> TrendPoints { get; set; } = new();
    public bool HasNegativeTrend { get; set; }
    public string TrendAssessment { get; set; }
    public Dictionary<string, double> ModuleTrends { get; set; } = new();
}

public class RiskAssessment
{
    public string OverallRiskLevel { get; set; }
    public List<CoverageRisk> IdentifiedRisks { get; set; } = new();
    public Dictionary<string, string> ModuleRiskLevels { get; set; } = new();
    public int TotalRiskScore { get; set; }
    public List<string> HighRiskAreas { get; set; } = new();
}

public class CoverageRisk
{
    public string Area { get; set; }
    public string RiskLevel { get; set; }
    public string Description { get; set; }
    public string Mitigation { get; set; }
    public int Impact { get; set; }
    public int Probability { get; set; }
}