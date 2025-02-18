using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates detailed documentation coverage reports
    /// </summary>
    public class DocumentationReportGenerator
    {
        private readonly IFileOperations _fileOps;

        public DocumentationReportGenerator(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        public async Task GenerateReportAsync(
            DocumentationValidationResult validationResult,
            PluginDocumentationReport pluginReport)
        {
            // Generate main coverage report
            await GenerateCoverageReportAsync(validationResult, pluginReport);

            // Generate issues report
            await GenerateIssuesReportAsync(validationResult, pluginReport);

            // Generate recommendations report
            await GenerateRecommendationsReportAsync(validationResult, pluginReport);
        }

        private async Task GenerateCoverageReportAsync(
            DocumentationValidationResult validationResult,
            PluginDocumentationReport pluginReport)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Documentation Coverage Report");
            sb.AppendLine();
            sb.AppendLine("## Overall Coverage");
            sb.AppendLine();
            sb.AppendLine("| Metric | Coverage | Status |");
            sb.AppendLine("|--------|----------|---------|");
            
            AddCoverageRow(sb, "Public API Documentation", validationResult.Coverage.PublicApiCoverage, 0.8);
            AddCoverageRow(sb, "Parameter Documentation", validationResult.Coverage.ParameterDocumentationCoverage, 0.9);
            AddCoverageRow(sb, "Code Examples", validationResult.Coverage.CodeExampleCoverage, 0.5);
            AddCoverageRow(sb, "Interface Documentation", validationResult.Coverage.InterfaceImplementationCoverage, 0.8);
            AddCoverageRow(sb, "Cross-Reference Validity", validationResult.Coverage.CrossReferenceValidity, 0.95);
            
            sb.AppendLine();
            sb.AppendLine("## Plugin Documentation");
            sb.AppendLine();
            sb.AppendLine("| Plugin | Version | Completeness | Status |");
            sb.AppendLine("|--------|----------|--------------|---------|");

            foreach (var plugin in pluginReport.PluginValidations.OrderBy(p => p.CompletenessScore))
            {
                AddPluginRow(sb, plugin);
            }

            await _fileOps.WriteFileAsync("docs/reports/documentation-coverage.md", sb.ToString());
        }

        private async Task GenerateIssuesReportAsync(
            DocumentationValidationResult validationResult,
            PluginDocumentationReport pluginReport)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Documentation Issues");
            sb.AppendLine();

            if (validationResult.Issues.Any())
            {
                sb.AppendLine("## General Documentation Issues");
                sb.AppendLine();
                sb.AppendLine("| Component | Type | Description | Severity |");
                sb.AppendLine("|-----------|------|-------------|-----------|");

                foreach (var issue in validationResult.Issues.OrderByDescending(i => i.Severity))
                {
                    sb.AppendLine($"| {issue.Component} | {issue.IssueType} | {issue.Description} | {issue.Severity} |");
                }
            }

            if (pluginReport.PluginValidations.Any(p => p.CompletenessScore < 0.8))
            {
                sb.AppendLine();
                sb.AppendLine("## Plugin Documentation Issues");
                sb.AppendLine();

                foreach (var plugin in pluginReport.PluginValidations.Where(p => p.CompletenessScore < 0.8))
                {
                    sb.AppendLine($"### {plugin.PluginName}");
                    sb.AppendLine();
                    sb.AppendLine("| Issue | Impact |");
                    sb.AppendLine("|-------|---------|");

                    if (!plugin.HasTypeDocumentation)
                        sb.AppendLine("| Missing plugin class documentation | Reduces plugin discoverability |");
                    
                    if (!plugin.HasExamples)
                        sb.AppendLine("| No usage examples | Makes plugin harder to use |");
                    
                    if (plugin.ConfigurationDocumentation?.Completeness < 1.0)
                        sb.AppendLine("| Incomplete configuration documentation | Configuration becomes unclear |");
                    
                    if (plugin.InterfaceDocumentation?.Completeness < 1.0)
                        sb.AppendLine("| Incomplete interface documentation | Integration becomes difficult |");
                    
                    if (plugin.DependencyDocumentation?.Completeness < 1.0)
                        sb.AppendLine("| Missing dependency documentation | Deployment issues may occur |");
                    
                    sb.AppendLine();
                }
            }

            await _fileOps.WriteFileAsync("docs/reports/documentation-issues.md", sb.ToString());
        }

        private async Task GenerateRecommendationsReportAsync(
            DocumentationValidationResult validationResult,
            PluginDocumentationReport pluginReport)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Documentation Improvement Recommendations");
            sb.AppendLine();

            var allRecommendations = validationResult.Recommendations
                .Concat(pluginReport.Recommendations)
                .OrderByDescending(r => r.Priority);

            foreach (var priority in Enum.GetValues<RecommendationPriority>())
            {
                var priorityRecs = allRecommendations.Where(r => r.Priority == priority);
                if (!priorityRecs.Any()) continue;

                sb.AppendLine($"## {priority} Priority");
                sb.AppendLine();
                sb.AppendLine("| Area | Recommendation | Impact |");
                sb.AppendLine("|------|----------------|---------|");

                foreach (var rec in priorityRecs)
                {
                    sb.AppendLine($"| {rec.Area} | {rec.Description} | {rec.Impact} |");
                    
                    if (rec.Details?.Any() == true)
                    {
                        sb.AppendLine();
                        foreach (var detail in rec.Details)
                        {
                            sb.AppendLine($"- {detail}");
                        }
                        sb.AppendLine();
                    }
                }

                sb.AppendLine();
            }

            await _fileOps.WriteFileAsync("docs/reports/documentation-recommendations.md", sb.ToString());
        }

        private void AddCoverageRow(StringBuilder sb, string metric, double coverage, double threshold)
        {
            var status = coverage >= threshold ? "✅" : "❌";
            sb.AppendLine($"| {metric} | {coverage:P0} | {status} |");
        }

        private void AddPluginRow(StringBuilder sb, PluginValidationResult plugin)
        {
            var status = plugin.CompletenessScore >= 0.8 ? "✅" : "❌";
            sb.AppendLine($"| {plugin.PluginName} | {plugin.Version} | {plugin.CompletenessScore:P0} | {status} |");
        }
    }
}