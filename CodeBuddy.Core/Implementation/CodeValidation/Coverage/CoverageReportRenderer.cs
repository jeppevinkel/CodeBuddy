using System;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.TestCoverage;
using System.Text.Json;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class CoverageReportRenderer : ICoverageReportRenderer
{
    public async Task<string> RenderHtmlReportAsync(TestCoverageReport report)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <title>CodeBuddy Test Coverage Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        .header { background: #f8f9fa; padding: 20px; border-radius: 5px; }");
        sb.AppendLine("        .module { margin: 20px 0; padding: 15px; border: 1px solid #ddd; }");
        sb.AppendLine("        .coverage-bar { height: 20px; background: #e9ecef; margin: 10px 0; }");
        sb.AppendLine("        .coverage-fill { height: 100%; background: #28a745; }");
        sb.AppendLine("        .uncovered { background: #dc3545; }");
        sb.AppendLine("        .warning { color: #856404; background: #fff3cd; padding: 10px; }");
        sb.AppendLine("        .trend-chart { width: 100%; height: 300px; margin: 20px 0; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Overall Summary
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"    <h1>Test Coverage Report</h1>");
        sb.AppendLine($"    <h2>Overall Coverage: {report.OverallCoveragePercentage:F1}%</h2>");
        sb.AppendLine($"    <div class=\"coverage-bar\">");
        sb.AppendLine($"        <div class=\"coverage-fill\" style=\"width: {report.OverallCoveragePercentage}%\"></div>");
        sb.AppendLine($"    </div>");
        sb.AppendLine("</div>");

        // Module Coverage
        sb.AppendLine("<h2>Module Coverage</h2>");
        foreach (var module in report.CoverageByModule.OrderByDescending(m => m.Value.CoveragePercentage))
        {
            sb.AppendLine("<div class=\"module\">");
            sb.AppendLine($"    <h3>{module.Key}</h3>");
            sb.AppendLine($"    <p>Coverage: {module.Value.CoveragePercentage:F1}%</p>");
            sb.AppendLine($"    <div class=\"coverage-bar\">");
            sb.AppendLine($"        <div class=\"coverage-fill\" style=\"width: {module.Value.CoveragePercentage}%\"></div>");
            sb.AppendLine($"    </div>");
            
            // Function Coverage
            if (module.Value.FunctionCoverage.Any())
            {
                sb.AppendLine("    <h4>Function Coverage</h4>");
                foreach (var func in module.Value.FunctionCoverage)
                {
                    sb.AppendLine($"    <p>{func.Key}: {func.Value:F1}%</p>");
                }
            }
            sb.AppendLine("</div>");
        }

        // Branch Coverage
        sb.AppendLine("<h2>Branch Coverage</h2>");
        sb.AppendLine("<div class=\"module\">");
        sb.AppendLine($"    <p>Coverage: {report.BranchCoverage.BranchCoveragePercentage:F1}%</p>");
        sb.AppendLine($"    <p>Total Branches: {report.BranchCoverage.TotalBranches}</p>");
        sb.AppendLine($"    <p>Covered Branches: {report.BranchCoverage.CoveredBranches}</p>");
        if (report.BranchCoverage.UncoveredBranches.Any())
        {
            sb.AppendLine("    <h4>Uncovered Branches</h4>");
            foreach (var branch in report.BranchCoverage.UncoveredBranches)
            {
                sb.AppendLine($"    <p class=\"warning\">{branch.Location}: {branch.Condition}</p>");
            }
        }
        sb.AppendLine("</div>");

        // Uncovered Sections
        if (report.UncoveredSections.Any())
        {
            sb.AppendLine("<h2>Uncovered Code Sections</h2>");
            foreach (var section in report.UncoveredSections)
            {
                sb.AppendLine("<div class=\"module uncovered\">");
                sb.AppendLine($"    <h4>{section.FilePath}</h4>");
                sb.AppendLine($"    <p>Lines {section.StartLine}-{section.EndLine}</p>");
                sb.AppendLine($"    <pre>{section.CodeBlock}</pre>");
                if (!string.IsNullOrEmpty(section.Reason))
                {
                    sb.AppendLine($"    <p>Reason: {section.Reason}</p>");
                }
                sb.AppendLine("</div>");
            }
        }

        // Coverage Trends
        if (report.CoverageTrends.Any())
        {
            sb.AppendLine("<h2>Coverage Trends</h2>");
            sb.AppendLine("<div class=\"module\">");
            sb.AppendLine("    <div class=\"trend-chart\">");
            sb.AppendLine("        <canvas id=\"trendChart\"></canvas>");
            sb.AppendLine("    </div>");
            
            // Add Chart.js
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        const ctx = document.getElementById('trendChart');");
            sb.AppendLine("        new Chart(ctx, {");
            sb.AppendLine("            type: 'line',");
            sb.AppendLine("            data: {");
            sb.AppendLine($"                labels: [{string.Join(",", report.CoverageTrends.Select(t => $"'{t.Timestamp:MM/dd}\'"))}],");
            sb.AppendLine("                datasets: [{");
            sb.AppendLine("                    label: 'Overall Coverage',");
            sb.AppendLine($"                    data: [{string.Join(",", report.CoverageTrends.Select(t => t.OverallCoverage))}],");
            sb.AppendLine("                    borderColor: 'rgb(75, 192, 192)',");
            sb.AppendLine("                    tension: 0.1");
            sb.AppendLine("                }]");
            sb.AppendLine("            },");
            sb.AppendLine("            options: {");
            sb.AppendLine("                responsive: true,");
            sb.AppendLine("                scales: {");
            sb.AppendLine("                    y: {");
            sb.AppendLine("                        beginAtZero: true,");
            sb.AppendLine("                        max: 100");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        });");
            sb.AppendLine("    </script>");
            sb.AppendLine("</div>");
        }

        // Recommendations
        if (report.Recommendations.Any())
        {
            sb.AppendLine("<h2>Recommendations</h2>");
            foreach (var rec in report.Recommendations)
            {
                sb.AppendLine("<div class=\"module\">");
                sb.AppendLine($"    <h4>{rec.ModuleName}</h4>");
                sb.AppendLine($"    <p><strong>Priority:</strong> {rec.Priority}</p>");
                sb.AppendLine($"    <p><strong>Potential Coverage Gain:</strong> {rec.PotentialCoverageGain:F1}%</p>");
                sb.AppendLine($"    <p>{rec.Recommendation}</p>");
                sb.AppendLine($"    <p><strong>Impact:</strong> {rec.Impact}</p>");
                sb.AppendLine("</div>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return await Task.FromResult(sb.ToString());
    }

    public async Task<string> RenderJsonReportAsync(TestCoverageReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return await Task.FromResult(JsonSerializer.Serialize(report, options));
    }
}