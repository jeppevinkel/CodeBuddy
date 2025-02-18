using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class RecommendationEngine : IRecommendationEngine
{
    private const double HIGH_IMPACT_THRESHOLD = 20.0;
    private const double MEDIUM_IMPACT_THRESHOLD = 10.0;

    public async Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report)
    {
        return await GenerateRecommendationsAsync(report, report.CoverageByModule.Keys.ToList());
    }

    public async Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report, List<string> targetModules)
    {
        var recommendations = new List<CoverageRecommendation>();

        foreach (var module in report.CoverageByModule.Where(m => targetModules.Contains(m.Key)))
        {
            // Check module coverage
            if (module.Value.CoveragePercentage < 80)
            {
                var potentialGain = 80 - module.Value.CoveragePercentage;
                var priority = GetPriorityBasedOnGap(potentialGain);

                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = module.Key,
                    Recommendation = $"Increase test coverage for module {module.Key}",
                    Impact = $"Improving coverage will reduce risk of regressions and improve code quality",
                    PotentialCoverageGain = potentialGain,
                    Priority = priority
                });
            }

            // Check function coverage
            foreach (var function in module.Value.FunctionCoverage.Where(f => f.Value < 70))
            {
                var potentialGain = 70 - function.Value;
                var priority = GetPriorityBasedOnGap(potentialGain);

                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = module.Key,
                    Recommendation = $"Add tests for function {function.Key}",
                    Impact = $"Function may contain critical business logic needing validation",
                    PotentialCoverageGain = potentialGain,
                    Priority = priority
                });
            }
        }

        // Check uncovered sections
        foreach (var section in report.UncoveredSections.Where(s => 
            targetModules.Any(m => s.FilePath.Contains(m))))
        {
            var linesCount = section.EndLine - section.StartLine + 1;
            var potentialGain = (linesCount * 100.0) / GetTotalLinesCount(report, section.FilePath);

            if (potentialGain >= 5) // Only recommend if impact is significant
            {
                var priority = GetPriorityBasedOnGap(potentialGain);

                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = GetModuleFromPath(section.FilePath),
                    Recommendation = $"Add tests for uncovered code block at lines {section.StartLine}-{section.EndLine}",
                    Impact = $"Large code block lacking any test coverage",
                    PotentialCoverageGain = potentialGain,
                    Priority = priority
                });
            }
        }

        // Check branch coverage gaps
        if (report.BranchCoverage.BranchCoveragePercentage < 75)
        {
            foreach (var branch in report.BranchCoverage.UncoveredBranches.Where(b => 
                targetModules.Any(m => b.Location.Contains(m))))
            {
                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = GetModuleFromPath(branch.Location),
                    Recommendation = $"Add test cases for uncovered branch conditions at {branch.Location}",
                    Impact = "Untested conditional logic may hide bugs",
                    PotentialCoverageGain = 100.0 / report.BranchCoverage.TotalBranches,
                    Priority = "High"
                });
            }
        }

        // Analyze historical trends
        if (report.CoverageTrends.Count >= 2)
        {
            var latestTrend = report.CoverageTrends.OrderByDescending(t => t.Timestamp).Take(2).ToList();
            if (latestTrend[0].OverallCoverage < latestTrend[1].OverallCoverage)
            {
                var coverage = latestTrend[0].OverallCoverage;
                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = "Overall",
                    Recommendation = "Coverage is trending downward. Review recent changes and add missing tests.",
                    Impact = "Declining test coverage may indicate technical debt accumulation",
                    PotentialCoverageGain = latestTrend[1].OverallCoverage - coverage,
                    Priority = "High"
                });
            }
        }

        return await Task.FromResult(recommendations.OrderByDescending(r => r.PotentialCoverageGain).ToList());
    }

    private string GetPriorityBasedOnGap(double gap)
    {
        if (gap >= HIGH_IMPACT_THRESHOLD) return "High";
        if (gap >= MEDIUM_IMPACT_THRESHOLD) return "Medium";
        return "Low";
    }

    private int GetTotalLinesCount(TestCoverageReport report, string filePath)
    {
        var module = report.CoverageByModule.FirstOrDefault(m => m.Value.FilePath == filePath);
        return module.Value?.LineByLineCoverage.Count ?? 100; // Default to 100 if unknown
    }

    private string GetModuleFromPath(string filePath)
    {
        // Extract module name from file path
        var parts = filePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[parts.Length - 2] : filePath;
    }
}