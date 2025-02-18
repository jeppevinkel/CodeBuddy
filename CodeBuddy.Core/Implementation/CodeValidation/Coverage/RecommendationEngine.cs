using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class RecommendationEngine : IRecommendationEngine
{
    private readonly IComplexityAnalyzer _complexityAnalyzer;
    private readonly IRiskAssessor _riskAssessor;

    public RecommendationEngine(
        IComplexityAnalyzer complexityAnalyzer,
        IRiskAssessor riskAssessor)
    {
        _complexityAnalyzer = complexityAnalyzer;
        _riskAssessor = riskAssessor;
    }

    public async Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report)
    {
        return await GenerateRecommendationsAsync(report, report.CoverageByModule.Keys.ToList());
    }

    public async Task<List<CoverageRecommendation>> GenerateRecommendationsAsync(TestCoverageReport report, List<string> targetModules)
    {
        var recommendations = new List<CoverageRecommendation>();

        foreach (var moduleName in targetModules)
        {
            if (report.CoverageByModule.TryGetValue(moduleName, out var moduleCoverage))
            {
                // Get complexity metrics for the module
                var complexityMetrics = await _complexityAnalyzer.AnalyzeComplexityAsync(moduleCoverage);
                
                // Assess risk based on coverage and complexity
                var riskAssessment = await _riskAssessor.AssessRiskAsync(moduleCoverage, complexityMetrics);

                // Generate recommendations based on analysis
                var moduleRecommendations = GenerateModuleRecommendations(
                    moduleName,
                    moduleCoverage,
                    complexityMetrics,
                    riskAssessment);

                recommendations.AddRange(moduleRecommendations);
            }
        }

        // Sort recommendations by priority and potential impact
        recommendations.Sort((a, b) =>
        {
            var priorityComparison = ComparePriorities(a.Priority, b.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            return b.PotentialCoverageGain.CompareTo(a.PotentialCoverageGain);
        });

        return recommendations;
    }

    private List<CoverageRecommendation> GenerateModuleRecommendations(
        string moduleName,
        ModuleCoverage coverage,
        ComplexityMetrics complexity,
        RiskAssessment risk)
    {
        var recommendations = new List<CoverageRecommendation>();

        // Check for complex methods with low coverage
        foreach (var function in complexity.FunctionComplexity)
        {
            if (function.Value > 10 && // High complexity threshold
                coverage.FunctionCoverage.TryGetValue(function.Key, out var functionCoverage) &&
                functionCoverage < 80) // Low coverage threshold
            {
                recommendations.Add(new CoverageRecommendation
                {
                    ModuleName = moduleName,
                    Recommendation = $"Increase test coverage for complex method '{function.Key}'",
                    Impact = "High complexity methods have higher risk of defects",
                    PotentialCoverageGain = CalculatePotentialGain(coverage, function.Key),
                    Priority = "High"
                });
            }
        }

        // Check for uncovered branches
        var uncoveredBranches = coverage.LineByLineCoverage
            .Where(l => !l.IsCovered && !l.IsExcluded)
            .ToList();

        if (uncoveredBranches.Any())
        {
            recommendations.Add(new CoverageRecommendation
            {
                ModuleName = moduleName,
                Recommendation = $"Add tests for {uncoveredBranches.Count} uncovered code branches",
                Impact = "Uncovered branches may hide defects",
                PotentialCoverageGain = CalculateUncoveredBranchesGain(coverage, uncoveredBranches.Count),
                Priority = risk.Level
            });
        }

        // Risk-based recommendations
        if (risk.Level == "High")
        {
            recommendations.Add(new CoverageRecommendation
            {
                ModuleName = moduleName,
                Recommendation = "Prioritize increasing coverage due to high risk assessment",
                Impact = risk.Explanation,
                PotentialCoverageGain = 100 - coverage.CoveragePercentage,
                Priority = "High"
            });
        }

        return recommendations;
    }

    private int ComparePriorities(string priorityA, string priorityB)
    {
        var priorityOrder = new Dictionary<string, int>
        {
            ["High"] = 0,
            ["Medium"] = 1,
            ["Low"] = 2
        };

        if (priorityOrder.TryGetValue(priorityA, out var orderA) &&
            priorityOrder.TryGetValue(priorityB, out var orderB))
        {
            return orderA.CompareTo(orderB);
        }

        return 0;
    }

    private double CalculatePotentialGain(ModuleCoverage coverage, string functionName)
    {
        if (coverage.FunctionCoverage.TryGetValue(functionName, out var functionCoverage))
        {
            return (100 - functionCoverage) * 0.01 * coverage.CoveragePercentage;
        }
        return 0;
    }

    private double CalculateUncoveredBranchesGain(ModuleCoverage coverage, int uncoveredBranchCount)
    {
        var totalLines = coverage.LineByLineCoverage.Count;
        if (totalLines == 0) return 0;

        return (uncoveredBranchCount * 100.0) / totalLines;
    }
}

public interface IComplexityAnalyzer
{
    Task<ComplexityMetrics> AnalyzeComplexityAsync(ModuleCoverage coverage);
}

public interface IRiskAssessor
{
    Task<RiskAssessment> AssessRiskAsync(ModuleCoverage coverage, ComplexityMetrics complexity);
}

public class ComplexityMetrics
{
    public Dictionary<string, int> FunctionComplexity { get; set; } = new();
    public int OverallComplexity { get; set; }
    public Dictionary<string, List<string>> Dependencies { get; set; } = new();
}

public class RiskAssessment
{
    public string Level { get; set; }
    public string Explanation { get; set; }
    public Dictionary<string, double> RiskFactors { get; set; } = new();
}