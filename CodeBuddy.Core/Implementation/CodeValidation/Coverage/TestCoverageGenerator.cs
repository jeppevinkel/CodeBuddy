using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public interface ITestCoverageGenerator
{
    Task<TestCoverageReport> GenerateReportAsync(ValidationContext context);
    Task<string> GenerateHtmlReportAsync(TestCoverageReport report);
    Task<string> GenerateJsonReportAsync(TestCoverageReport report);
    Task ValidateCoverageThresholdsAsync(TestCoverageReport report, ValidationResult result);
}

public class TestCoverageGenerator : ITestCoverageGenerator
{
    private readonly ICoverageDataCollector _dataCollector;
    private readonly ICoverageReportRenderer _reportRenderer;
    private readonly ICoverageTrendAnalyzer _trendAnalyzer;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly CoverageThresholdConfig _thresholdConfig;

    public TestCoverageGenerator(
        ICoverageDataCollector dataCollector,
        ICoverageReportRenderer reportRenderer,
        ICoverageTrendAnalyzer trendAnalyzer,
        IRecommendationEngine recommendationEngine,
        CoverageThresholdConfig thresholdConfig)
    {
        _dataCollector = dataCollector;
        _reportRenderer = reportRenderer;
        _trendAnalyzer = trendAnalyzer;
        _recommendationEngine = recommendationEngine;
    }

    public async Task<TestCoverageReport> GenerateReportAsync(ValidationContext context)
    {
        // Collect coverage data
        var coverageData = await _dataCollector.CollectCoverageDataAsync(context);
        
        // Generate basic report
        var report = new TestCoverageReport
        {
            OverallCoveragePercentage = coverageData.CalculateOverallCoverage(),
            CoverageByModule = coverageData.GetModuleCoverage(),
            BranchCoverage = coverageData.CalculateBranchCoverage(),
            StatementCoverage = coverageData.CalculateStatementCoverage(),
            UncoveredSections = coverageData.IdentifyUncoveredSections(),
        };

        // Add historical trends
        report.CoverageTrends = await _trendAnalyzer.AnalyzeTrendsAsync(context);

        // Generate recommendations
        report.Recommendations = await _recommendationEngine.GenerateRecommendationsAsync(report);

        return report;
    }

    public async Task<string> GenerateHtmlReportAsync(TestCoverageReport report)
    {
        return await _reportRenderer.RenderHtmlReportAsync(report);
    }

    public async Task<string> GenerateJsonReportAsync(TestCoverageReport report)
    {
        return await _reportRenderer.RenderJsonReportAsync(report);
    }

    public async Task ValidateCoverageThresholdsAsync(TestCoverageReport report, ValidationResult result)
    {
        var validation = new CoverageValidationResult
        {
            ThresholdPercentage = _thresholdConfig.MinimumOverallCoverage,
            ModuleThresholds = _thresholdConfig.ModuleThresholds.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.MinimumCoverage)
        };

        // Validate overall coverage requirements
        validation.MeetsThreshold = ValidateOverallCoverage(report);
        
        // Validate branch and statement coverage
        var branchCoverageValid = report.BranchCoverage.BranchCoveragePercentage >= _thresholdConfig.MinimumBranchCoverage;
        var statementCoverageValid = report.StatementCoverage.StatementCoveragePercentage >= _thresholdConfig.MinimumStatementCoverage;
        
        validation.DetailedValidation = new Dictionary<string, bool>
        {
            { "OverallCoverage", validation.MeetsThreshold },
            { "BranchCoverage", branchCoverageValid },
            { "StatementCoverage", statementCoverageValid }
        };

        // Validate per-module coverage
        validation.ModulesBelowThreshold = ValidateModuleCoverage(report);
        
        // Handle legacy code if enabled
        if (_thresholdConfig.LegacyCodeSettings.EnableLegacyCodeDifferentiation)
        {
            validation.LegacyCodeValidation = ValidateLegacyCode(report);
        }

        // Apply exclusions
        ApplyExclusions(validation);

        // Generate improvement suggestions
        validation.ImprovementSuggestions = await _recommendationEngine
            .GenerateRecommendationsAsync(report, validation.ModulesBelowThreshold);

        result.CoverageValidation = validation;
    }

    private bool ValidateOverallCoverage(TestCoverageReport report)
    {
        return report.OverallCoveragePercentage >= _thresholdConfig.MinimumOverallCoverage;
    }

    private List<string> ValidateModuleCoverage(TestCoverageReport report)
    {
        var modulesBelowThreshold = new List<string>();

        foreach (var module in report.CoverageByModule)
        {
            var threshold = _thresholdConfig.ModuleThresholds.TryGetValue(module.Key, out var moduleConfig)
                ? moduleConfig.MinimumCoverage
                : _thresholdConfig.MinimumOverallCoverage;

            if (module.Value.CoveragePercentage < threshold)
            {
                modulesBelowThreshold.Add(module.Key);
            }
        }

        return modulesBelowThreshold;
    }

    private Dictionary<string, bool> ValidateLegacyCode(TestCoverageReport report)
    {
        var validation = new Dictionary<string, bool>();
        var legacyThreshold = _thresholdConfig.LegacyCodeSettings.LegacyCodeMinimumCoverage;
        var newCodeThreshold = _thresholdConfig.LegacyCodeSettings.NewCodeMinimumCoverage;
        var ageThreshold = DateTime.Now.AddDays(-_thresholdConfig.LegacyCodeSettings.LegacyCodeAgeThresholdDays);

        foreach (var trend in report.CoverageTrends)
        {
            var isLegacy = trend.Timestamp < ageThreshold;
            var threshold = isLegacy ? legacyThreshold : newCodeThreshold;
            validation[trend.CommitId] = trend.OverallCoverage >= threshold;
        }

        return validation;
    }

    private void ApplyExclusions(CoverageValidationResult validation)
    {
        if (_thresholdConfig.Exclusions?.ExcludedPaths == null) return;

        validation.ModulesBelowThreshold = validation.ModulesBelowThreshold
            .Where(module => !_thresholdConfig.Exclusions.ExcludedPaths.Any(path => 
                module.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}

public interface ICoverageDataCollector
{
    Task<CoverageData> CollectCoverageDataAsync(ValidationContext context);
}

public interface ICoverageReportRenderer
{
    Task<string> RenderHtmlReportAsync(TestCoverageReport report);
    Task<string> RenderJsonReportAsync(TestCoverageReport report);
}

public interface ICoverageTrendAnalyzer
{
    Task<List<CoverageTrendPoint>> AnalyzeTrendsAsync(ValidationContext context);
}

public interface IRecommendationEngine
{
    Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report);
    Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report, List<string> targetModules);
}

public class CoverageData
{
    public Dictionary<string, ModuleCoverage> ModuleCoverageData { get; set; } = new();
    public Dictionary<string, List<LineCoverage>> LineCoverageData { get; set; } = new();
    public Dictionary<string, List<BranchInfo>> BranchData { get; set; } = new();

    public double CalculateOverallCoverage()
    {
        if (!ModuleCoverageData.Any())
            return 0;

        return ModuleCoverageData.Values.Average(m => m.CoveragePercentage);
    }

    public Dictionary<string, ModuleCoverage> GetModuleCoverage()
    {
        return ModuleCoverageData;
    }

    public BranchCoverageStatistics CalculateBranchCoverage()
    {
        var totalBranches = 0;
        var coveredBranches = 0;
        var uncoveredBranches = new List<BranchInfo>();

        foreach (var moduleData in BranchData)
        {
            foreach (var branch in moduleData.Value)
            {
                totalBranches++;
                if (branch.BranchOutcomes.All(o => o.Value))
                    coveredBranches++;
                else
                    uncoveredBranches.Add(branch);
            }
        }

        return new BranchCoverageStatistics
        {
            TotalBranches = totalBranches,
            CoveredBranches = coveredBranches,
            BranchCoveragePercentage = totalBranches > 0 ? (coveredBranches * 100.0 / totalBranches) : 0,
            UncoveredBranches = uncoveredBranches
        };
    }

    public StatementCoverageMetrics CalculateStatementCoverage()
    {
        var totalStatements = 0;
        var coveredStatements = 0;
        var distribution = new Dictionary<string, int>();

        foreach (var moduleData in LineCoverageData)
        {
            foreach (var line in moduleData.Value)
            {
                if (!line.IsExcluded)
                {
                    totalStatements++;
                    if (line.IsCovered)
                        coveredStatements++;
                }
            }
        }

        return new StatementCoverageMetrics
        {
            TotalStatements = totalStatements,
            CoveredStatements = coveredStatements,
            StatementCoveragePercentage = totalStatements > 0 ? (coveredStatements * 100.0 / totalStatements) : 0,
            StatementTypeDistribution = distribution
        };
    }

    public List<UncoveredCodeSection> IdentifyUncoveredSections()
    {
        var uncoveredSections = new List<UncoveredCodeSection>();

        foreach (var moduleData in LineCoverageData)
        {
            var currentSection = new UncoveredCodeSection
            {
                FilePath = moduleData.Key,
                StartLine = -1
            };

            var lines = moduleData.Value.OrderBy(l => l.LineNumber).ToList();
            
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                
                if (!line.IsCovered && !line.IsExcluded)
                {
                    if (currentSection.StartLine == -1)
                    {
                        currentSection.StartLine = line.LineNumber;
                        currentSection.CodeBlock = line.Content;
                    }
                    else
                    {
                        currentSection.CodeBlock += "\n" + line.Content;
                    }
                }
                else if (currentSection.StartLine != -1)
                {
                    currentSection.EndLine = lines[i - 1].LineNumber;
                    uncoveredSections.Add(currentSection);
                    
                    currentSection = new UncoveredCodeSection
                    {
                        FilePath = moduleData.Key,
                        StartLine = -1
                    };
                }
            }

            if (currentSection.StartLine != -1)
            {
                currentSection.EndLine = lines[^1].LineNumber;
                uncoveredSections.Add(currentSection);
            }
        }

        return uncoveredSections;
    }
}