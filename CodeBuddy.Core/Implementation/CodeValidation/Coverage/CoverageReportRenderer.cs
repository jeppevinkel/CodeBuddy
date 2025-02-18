using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.TestCoverage;

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
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Code Coverage Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        .module { margin-bottom: 20px; border: 1px solid #ddd; padding: 10px; }");
        sb.AppendLine("        .covered { background-color: #DFF0D8; }");
        sb.AppendLine("        .uncovered { background-color: #F2DEDE; }");
        sb.AppendLine("        .branch { margin: 5px 0; padding: 5px; background-color: #FCF8E3; }");
        sb.AppendLine("        .progress { background-color: #f5f5f5; height: 20px; margin-bottom: 10px; }");
        sb.AppendLine("        .progress-bar { background-color: #5cb85c; height: 100%; text-align: center; color: white; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Overall Summary
        sb.AppendLine("<h1>Code Coverage Report</h1>");
        sb.AppendLine($"<h2>Overall Coverage: {report.OverallCoveragePercentage:F1}%</h2>");
        sb.AppendLine("<div class=\"progress\">");
        sb.AppendLine($"    <div class=\"progress-bar\" style=\"width: {report.OverallCoveragePercentage}%\"></div>");
        sb.AppendLine("</div>");

        // Branch Coverage
        sb.AppendLine("<h3>Branch Coverage</h3>");
        sb.AppendLine($"<p>Coverage: {report.BranchCoverage.BranchCoveragePercentage:F1}% ({report.BranchCoverage.CoveredBranches}/{report.BranchCoverage.TotalBranches} branches)</p>");
        
        // Statement Coverage
        sb.AppendLine("<h3>Statement Coverage</h3>");
        sb.AppendLine($"<p>Coverage: {report.StatementCoverage.StatementCoveragePercentage:F1}% ({report.StatementCoverage.CoveredStatements}/{report.StatementCoverage.TotalStatements} statements)</p>");

        // Module Details
        sb.AppendLine("<h3>Module Coverage Details</h3>");
        foreach (var module in report.CoverageByModule)
        {
            sb.AppendLine("<div class=\"module\">");
            sb.AppendLine($"    <h4>{module.Key}</h4>");
            sb.AppendLine($"    <p>Coverage: {module.Value.CoveragePercentage:F1}%</p>");
            sb.AppendLine("    <div class=\"progress\">");
            sb.AppendLine($"        <div class=\"progress-bar\" style=\"width: {module.Value.CoveragePercentage}%\"></div>");
            sb.AppendLine("    </div>");
            
            // Line Coverage
            sb.AppendLine("    <div class=\"line-coverage\">");
            foreach (var line in module.Value.LineByLineCoverage)
            {
                var coverageClass = line.IsCovered ? "covered" : "uncovered";
                if (!line.IsExcluded)
                {
                    sb.AppendLine($"        <pre class=\"{coverageClass}\">{line.LineNumber}: {System.Web.HttpUtility.HtmlEncode(line.Content)}</pre>");
                }
            }
            sb.AppendLine("    </div>");
            sb.AppendLine("</div>");
        }

        // Uncovered Sections
        if (report.UncoveredSections.Count > 0)
        {
            sb.AppendLine("<h3>Uncovered Code Sections</h3>");
            foreach (var section in report.UncoveredSections)
            {
                sb.AppendLine("<div class=\"uncovered-section\">");
                sb.AppendLine($"    <p>File: {section.FilePath}</p>");
                sb.AppendLine($"    <p>Lines: {section.StartLine}-{section.EndLine}</p>");
                sb.AppendLine($"    <pre>{System.Web.HttpUtility.HtmlEncode(section.CodeBlock)}</pre>");
                sb.AppendLine("</div>");
            }
        }

        // Recommendations
        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine("<h3>Coverage Improvement Recommendations</h3>");
            foreach (var recommendation in report.Recommendations)
            {
                sb.AppendLine("<div class=\"recommendation\">");
                sb.AppendLine($"    <h4>{recommendation.ModuleName}</h4>");
                sb.AppendLine($"    <p><strong>Priority:</strong> {recommendation.Priority}</p>");
                sb.AppendLine($"    <p><strong>Potential Gain:</strong> {recommendation.PotentialCoverageGain:F1}%</p>");
                sb.AppendLine($"    <p>{recommendation.Recommendation}</p>");
                sb.AppendLine($"    <p><em>Impact: {recommendation.Impact}</em></p>");
                sb.AppendLine("</div>");
            }
        }

        // Footer
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return await Task.FromResult(sb.ToString());
    }

    public async Task<string> GenerateJsonReportAsync(TestCoverageReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return await Task.FromResult(JsonSerializer.Serialize(report, options));
    }
}