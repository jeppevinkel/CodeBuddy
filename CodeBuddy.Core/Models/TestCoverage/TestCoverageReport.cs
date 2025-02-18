using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.TestCoverage;

public class TestCoverageReport
{
    public double OverallCoveragePercentage { get; set; }
    public Dictionary<string, ModuleCoverage> CoverageByModule { get; set; } = new();
    public BranchCoverageStatistics BranchCoverage { get; set; } = new();
    public StatementCoverageMetrics StatementCoverage { get; set; } = new();
    public List<UncoveredCodeSection> UncoveredSections { get; set; } = new();
    public List<CoverageTrendPoint> CoverageTrends { get; set; } = new();
    public List<CoverageRecommendation> Recommendations { get; set; } = new();
}

public class ModuleCoverage
{
    public string ModuleName { get; set; }
    public string FilePath { get; set; }
    public double CoveragePercentage { get; set; }
    public List<LineCoverage> LineByLineCoverage { get; set; } = new();
    public List<string> ExcludedRegions { get; set; } = new();
    public Dictionary<string, double> FunctionCoverage { get; set; } = new();
}

public class LineCoverage
{
    public int LineNumber { get; set; }
    public string Content { get; set; }
    public bool IsCovered { get; set; }
    public int ExecutionCount { get; set; }
    public bool IsExcluded { get; set; }
}

public class BranchCoverageStatistics
{
    public double BranchCoveragePercentage { get; set; }
    public int TotalBranches { get; set; }
    public int CoveredBranches { get; set; }
    public List<BranchInfo> UncoveredBranches { get; set; } = new();
}

public class BranchInfo
{
    public string Location { get; set; }
    public string Condition { get; set; }
    public Dictionary<string, bool> BranchOutcomes { get; set; } = new();
}

public class StatementCoverageMetrics
{
    public double StatementCoveragePercentage { get; set; }
    public int TotalStatements { get; set; }
    public int CoveredStatements { get; set; }
    public Dictionary<string, int> StatementTypeDistribution { get; set; } = new();
}

public class UncoveredCodeSection
{
    public string FilePath { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string CodeBlock { get; set; }
    public string SectionType { get; set; }
    public string Reason { get; set; }
}

public class CoverageTrendPoint
{
    public DateTime Timestamp { get; set; }
    public double OverallCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double StatementCoverage { get; set; }
    public string CommitId { get; set; }
}

public class CoverageRecommendation
{
    public string ModuleName { get; set; }
    public string Recommendation { get; set; }
    public string Impact { get; set; }
    public double PotentialCoverageGain { get; set; }
    public string Priority { get; set; }
}