using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageReportRenderer : ICoverageReportRenderer
{
    private readonly CoverageThresholdConfig _thresholdConfig;
    private readonly string _templatePath;

    public CoverageReportRenderer(CoverageThresholdConfig thresholdConfig, string templatePath = null)
    {
        _thresholdConfig = thresholdConfig;
        _templatePath = templatePath ?? Path.Combine("Templates", "CoverageReport.html");
    }

    public async Task<string> RenderHtmlReportAsync(TestCoverageReport report)
    {
        var template = await File.ReadAllTextAsync(_templatePath);
        
        // Replace placeholders with actual data
        template = template.Replace("{{GENERATION_DATE}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        
        // Overall coverage metrics
        template = RenderOverallMetrics(template, report);
        
        // Module coverage table
        template = RenderModuleCoverage(template, report);
        
        // Trends data
        template = RenderTrends(template, report);
        
        // Recommendations
        template = RenderRecommendations(template, report);
        
        return template;
    }

    public async Task<string> RenderJsonReportAsync(TestCoverageReport report)
    {
        var jsonReport = new
        {
            report.OverallCoveragePercentage,
            report.BranchCoverage,
            report.StatementCoverage,
            ModuleCoverage = report.CoverageByModule,
            report.CoverageTrends,
            report.Recommendations,
            Thresholds = new
            {
                Overall = _thresholdConfig.MinimumOverallCoverage,
                Branch = _thresholdConfig.MinimumBranchCoverage,
                Statement = _thresholdConfig.MinimumStatementCoverage,
                ModuleSpecific = _thresholdConfig.ModuleThresholds
            }
        };

        return JsonSerializer.Serialize(jsonReport, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private string RenderOverallMetrics(string template, TestCoverageReport report)
    {
        template = template.Replace("{{OVERALL_COVERAGE}}", report.OverallCoveragePercentage.ToString("F1"));
        template = template.Replace("{{BRANCH_COVERAGE}}", report.BranchCoverage.BranchCoveragePercentage.ToString("F1"));
        template = template.Replace("{{STATEMENT_COVERAGE}}", report.StatementCoverage.StatementCoveragePercentage.ToString("F1"));

        // Add status classes
        template = template.Replace("{{OVERALL_COVERAGE_CLASS}}", GetMetricClass(report.OverallCoveragePercentage, _thresholdConfig.MinimumOverallCoverage));
        template = template.Replace("{{BRANCH_COVERAGE_CLASS}}", GetMetricClass(report.BranchCoverage.BranchCoveragePercentage, _thresholdConfig.MinimumBranchCoverage));
        template = template.Replace("{{STATEMENT_COVERAGE_CLASS}}", GetMetricClass(report.StatementCoverage.StatementCoveragePercentage, _thresholdConfig.MinimumStatementCoverage));

        return template;
    }

    private string RenderModuleCoverage(string template, TestCoverageReport report)
    {
        var tableRows = new StringBuilder();
        foreach (var module in report.CoverageByModule)
        {
            var threshold = _thresholdConfig.ModuleThresholds.TryGetValue(module.Key, out var moduleConfig)
                ? moduleConfig.MinimumCoverage
                : _thresholdConfig.MinimumOverallCoverage;

            var status = module.Value.CoveragePercentage >= threshold ? "✅" : "❌";
            
            tableRows.AppendLine($@"
                <tr>
                    <td>{module.Key}</td>
                    <td>{module.Value.CoveragePercentage:F1}%</td>
                    <td>{threshold:F1}%</td>
                    <td>{status}</td>
                </tr>");
        }

        return template.Replace("{{MODULE_COVERAGE_ROWS}}", tableRows.ToString());
    }

    private string RenderTrends(string template, TestCoverageReport report)
    {
        var labels = report.CoverageTrends.Select(t => t.Timestamp.ToString("MM/dd/yyyy")).ToList();
        var data = report.CoverageTrends.Select(t => t.OverallCoverage).ToList();

        template = template.Replace("{{TREND_LABELS}}", JsonSerializer.Serialize(labels));
        template = template.Replace("{{TREND_DATA}}", JsonSerializer.Serialize(data));

        return template;
    }

    private string RenderRecommendations(string template, TestCoverageReport report)
    {
        var recommendations = new StringBuilder();
        foreach (var recommendation in report.Recommendations)
        {
            recommendations.AppendLine($@"
                <div class='recommendation-item'>
                    <h4>{recommendation.ModuleName}</h4>
                    <p>{recommendation.Recommendation}</p>
                    <p><strong>Impact:</strong> {recommendation.Impact}</p>
                    <p><strong>Potential Gain:</strong> {recommendation.PotentialCoverageGain:F1}%</p>
                    <p><strong>Priority:</strong> {recommendation.Priority}</p>
                </div>");
        }

        return template.Replace("{{RECOMMENDATIONS}}", recommendations.ToString());
    }

    private string GetMetricClass(double value, double threshold)
    {
        if (value >= threshold) return "success";
        if (value >= threshold * 0.9) return "warning";
        return "danger";
    }
}