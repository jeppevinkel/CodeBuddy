using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public interface IUnifiedCoverageDashboard
{
    Task<UnifiedCoverageMetrics> GetUnifiedMetricsAsync(string projectPath);
    Task<List<CoverageTrendPoint>> GetCoverageTrendsAsync(string projectPath);
    Task<List<CoverageRecommendation>> GetRecommendationsAsync(string projectPath);
    Task<CoverageBranchComparison> CompareBranchCoverageAsync(string projectPath, string baseBranch, string targetBranch);
    Task<List<CoverageRegression>> DetectCoverageRegressionsAsync(string projectPath);
    Task<List<LowCoverageArea>> IdentifyLowCoverageAreasAsync(string projectPath);
    Task TrackCoverageProgressAsync(string projectPath, UnifiedCoverageMetrics metrics);
    Task<string> GenerateUnifiedReportAsync(string projectPath, string format = "html");
}

public class UnifiedCoverageDashboard : IUnifiedCoverageDashboard
{
    private readonly ITestCoverageGenerator _coverageGenerator;
    private readonly ICoverageTrendAnalyzer _trendAnalyzer;
    private readonly ICoverageHistoryRepository _historyRepository;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ISourceControlProvider _sourceControlProvider;

    public UnifiedCoverageDashboard(
        ITestCoverageGenerator coverageGenerator,
        ICoverageTrendAnalyzer trendAnalyzer,
        ICoverageHistoryRepository historyRepository,
        IRecommendationEngine recommendationEngine,
        ISourceControlProvider sourceControlProvider)
    {
        _coverageGenerator = coverageGenerator;
        _trendAnalyzer = trendAnalyzer;
        _historyRepository = historyRepository;
        _recommendationEngine = recommendationEngine;
        _sourceControlProvider = sourceControlProvider;
    }

    public async Task<UnifiedCoverageMetrics> GetUnifiedMetricsAsync(string projectPath)
    {
        var context = new ValidationContext { ProjectPath = projectPath };
        var report = await _coverageGenerator.GenerateReportAsync(context);
        var history = await _historyRepository.GetHistoricalCoverageAsync(projectPath);

        return new UnifiedCoverageMetrics
        {
            OverallCoverage = report.OverallCoveragePercentage,
            BranchCoverage = report.BranchCoverage.BranchCoveragePercentage,
            StatementCoverage = report.StatementCoverage.StatementCoveragePercentage,
            CoverageByModule = report.CoverageByModule,
            UncoveredSections = report.UncoveredSections,
            HistoricalTrend = history,
            LastUpdated = DateTime.UtcNow,
            CommitId = await _sourceControlProvider.GetCurrentCommitAsync()
        };
    }

    public async Task<List<CoverageTrendPoint>> GetCoverageTrendsAsync(string projectPath)
    {
        var context = new ValidationContext { ProjectPath = projectPath };
        return await _trendAnalyzer.AnalyzeTrendsAsync(context);
    }

    public async Task<List<CoverageRecommendation>> GetRecommendationsAsync(string projectPath)
    {
        var context = new ValidationContext { ProjectPath = projectPath };
        var report = await _coverageGenerator.GenerateReportAsync(context);
        
        // Focus recommendations on modules with coverage below 80%
        var lowCoverageModules = report.CoverageByModule
            .Where(m => m.Value.CoveragePercentage < 80)
            .Select(m => m.Key)
            .ToList();

        return await _recommendationEngine.GenerateRecommendationsAsync(report, lowCoverageModules);
    }

    public async Task<CoverageBranchComparison> CompareBranchCoverageAsync(string projectPath, string baseBranch, string targetBranch)
    {
        var baseContext = new ValidationContext 
        { 
            ProjectPath = projectPath,
            Branch = baseBranch
        };
        var targetContext = new ValidationContext 
        { 
            ProjectPath = projectPath,
            Branch = targetBranch
        };

        var baseReport = await _coverageGenerator.GenerateReportAsync(baseContext);
        var targetReport = await _coverageGenerator.GenerateReportAsync(targetContext);

        return new CoverageBranchComparison
        {
            BaseBranch = baseBranch,
            TargetBranch = targetBranch,
            CoverageDifference = targetReport.OverallCoveragePercentage - baseReport.OverallCoveragePercentage,
            ModuleDifferences = CompareModuleCoverage(baseReport.CoverageByModule, targetReport.CoverageByModule),
            NewUncoveredSections = targetReport.UncoveredSections
                .Where(t => !baseReport.UncoveredSections.Any(b => 
                    b.FilePath == t.FilePath && 
                    b.StartLine == t.StartLine && 
                    b.EndLine == t.EndLine))
                .ToList(),
            TimeStamp = DateTime.UtcNow
        };
    }

    public async Task<List<CoverageRegression>> DetectCoverageRegressionsAsync(string projectPath)
    {
        var regressions = new List<CoverageRegression>();
        var history = await _historyRepository.GetHistoricalCoverageAsync(projectPath);
        
        if (history.Count < 2)
            return regressions;

        var orderedHistory = history.OrderBy(h => h.Timestamp).ToList();
        var baseline = orderedHistory.First();
        var current = orderedHistory.Last();

        // Overall coverage regression
        if (current.OverallCoverage < baseline.OverallCoverage)
        {
            regressions.Add(new CoverageRegression
            {
                Type = "Overall",
                BaselineCoverage = baseline.OverallCoverage,
                CurrentCoverage = current.OverallCoverage,
                Difference = current.OverallCoverage - baseline.OverallCoverage,
                BaselineCommit = baseline.CommitId,
                CurrentCommit = current.CommitId,
                DetectedAt = DateTime.UtcNow,
                Severity = Math.Abs(current.OverallCoverage - baseline.OverallCoverage) > 5 ? "High" : "Medium"
            });
        }

        // Branch coverage regression
        if (current.BranchCoverage < baseline.BranchCoverage)
        {
            regressions.Add(new CoverageRegression
            {
                Type = "Branch",
                BaselineCoverage = baseline.BranchCoverage,
                CurrentCoverage = current.BranchCoverage,
                Difference = current.BranchCoverage - baseline.BranchCoverage,
                BaselineCommit = baseline.CommitId,
                CurrentCommit = current.CommitId,
                DetectedAt = DateTime.UtcNow,
                Severity = Math.Abs(current.BranchCoverage - baseline.BranchCoverage) > 5 ? "High" : "Medium"
            });
        }

        return regressions;
    }

    public async Task<List<LowCoverageArea>> IdentifyLowCoverageAreasAsync(string projectPath)
    {
        var context = new ValidationContext { ProjectPath = projectPath };
        var report = await _coverageGenerator.GenerateReportAsync(context);
        var lowCoverageAreas = new List<LowCoverageArea>();

        // Identify modules with coverage below 80%
        foreach (var module in report.CoverageByModule.Where(m => m.Value.CoveragePercentage < 80))
        {
            var area = new LowCoverageArea
            {
                Name = module.Key,
                Type = "Module",
                CurrentCoverage = module.Value.CoveragePercentage,
                UncoveredSections = report.UncoveredSections
                    .Where(s => s.FilePath.Contains(module.Key))
                    .ToList(),
                Risk = module.Value.CoveragePercentage < 50 ? "High" : "Medium",
                RecommendedActions = new List<string>()
            };

            // Add specific recommendations based on coverage patterns
            if (module.Value.FunctionCoverage.Any(f => f.Value < 60))
            {
                area.RecommendedActions.Add("Add unit tests for low-coverage functions");
            }
            if (report.BranchCoverage.UncoveredBranches.Any(b => b.Location.Contains(module.Key)))
            {
                area.RecommendedActions.Add("Add test cases for uncovered conditional branches");
            }

            lowCoverageAreas.Add(area);
        }

        return lowCoverageAreas;
    }

    public async Task TrackCoverageProgressAsync(string projectPath, UnifiedCoverageMetrics metrics)
    {
        await _historyRepository.SaveCoverageDataAsync(projectPath, new CoverageMetrics
        {
            OverallCoverage = metrics.OverallCoverage,
            BranchCoverage = metrics.BranchCoverage,
            StatementCoverage = metrics.StatementCoverage
        });
    }

    public async Task<string> GenerateUnifiedReportAsync(string projectPath, string format = "html")
    {
        var context = new ValidationContext { ProjectPath = projectPath };
        var report = await _coverageGenerator.GenerateReportAsync(context);
        
        return format.ToLower() switch
        {
            "html" => await _coverageGenerator.GenerateHtmlReportAsync(report),
            "json" => await _coverageGenerator.GenerateJsonReportAsync(report),
            _ => throw new ArgumentException("Unsupported report format", nameof(format))
        };
    }

    private static List<ModuleCoverageDifference> CompareModuleCoverage(
        Dictionary<string, ModuleCoverage> baseCoverage,
        Dictionary<string, ModuleCoverage> targetCoverage)
    {
        var differences = new List<ModuleCoverageDifference>();

        // Compare modules present in both branches
        foreach (var module in baseCoverage.Keys.Union(targetCoverage.Keys))
        {
            var baseExists = baseCoverage.TryGetValue(module, out var baseMod);
            var targetExists = targetCoverage.TryGetValue(module, out var targetMod);

            var diff = new ModuleCoverageDifference
            {
                ModuleName = module,
                BaselineCoverage = baseExists ? baseMod.CoveragePercentage : 0,
                CurrentCoverage = targetExists ? targetMod.CoveragePercentage : 0,
                Status = !baseExists ? "New" :
                        !targetExists ? "Removed" :
                        targetMod.CoveragePercentage < baseMod.CoveragePercentage ? "Decreased" :
                        targetMod.CoveragePercentage > baseMod.CoveragePercentage ? "Increased" : "Unchanged"
            };

            diff.Difference = diff.CurrentCoverage - diff.BaselineCoverage;
            differences.Add(diff);
        }

        return differences;
    }
}

public class UnifiedCoverageMetrics
{
    public double OverallCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double StatementCoverage { get; set; }
    public Dictionary<string, ModuleCoverage> CoverageByModule { get; set; }
    public List<UncoveredCodeSection> UncoveredSections { get; set; }
    public List<HistoricalCoveragePoint> HistoricalTrend { get; set; }
    public DateTime LastUpdated { get; set; }
    public string CommitId { get; set; }
}

public class CoverageBranchComparison
{
    public string BaseBranch { get; set; }
    public string TargetBranch { get; set; }
    public double CoverageDifference { get; set; }
    public List<ModuleCoverageDifference> ModuleDifferences { get; set; }
    public List<UncoveredCodeSection> NewUncoveredSections { get; set; }
    public DateTime TimeStamp { get; set; }
}

public class ModuleCoverageDifference
{
    public string ModuleName { get; set; }
    public double BaselineCoverage { get; set; }
    public double CurrentCoverage { get; set; }
    public double Difference { get; set; }
    public string Status { get; set; }
}

public class CoverageRegression
{
    public string Type { get; set; }
    public double BaselineCoverage { get; set; }
    public double CurrentCoverage { get; set; }
    public double Difference { get; set; }
    public string BaselineCommit { get; set; }
    public string CurrentCommit { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Severity { get; set; }
}

public class LowCoverageArea
{
    public string Name { get; set; }
    public string Type { get; set; }
    public double CurrentCoverage { get; set; }
    public List<UncoveredCodeSection> UncoveredSections { get; set; }
    public string Risk { get; set; }
    public List<string> RecommendedActions { get; set; }
}