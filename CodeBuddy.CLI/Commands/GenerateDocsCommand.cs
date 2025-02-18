using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.Documentation;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.CLI.Commands
{
    public class GenerateDocsCommand : Command
    {
        private readonly IServiceProvider _serviceProvider;

        public GenerateDocsCommand(IServiceProvider serviceProvider) 
            : base("generate-docs", "Generate documentation from code")
        {
            _serviceProvider = serviceProvider;

            var typeOption = new Option<string>(
                "--type",
                getDefaultValue: () => "all",
                description: "Type of documentation to generate (all, api, plugin, validation)");

            var coverageThresholdOption = new Option<double>(
                "--coverage-threshold",
                getDefaultValue: () => 80.0,
                description: "Minimum documentation coverage percentage required");

            var strictOption = new Option<bool>(
                "--strict",
                getDefaultValue: () => false,
                description: "Enforce strict documentation requirements");

            AddOption(typeOption);
            AddOption(coverageThresholdOption);
            AddOption(strictOption);

            this.SetHandler(HandleCommand);
        }

        private async Task HandleCommand(InvocationContext context)
        {
            var docType = context.ParseResult.GetValueForOption<string>("--type");
            var coverageThreshold = context.ParseResult.GetValueForOption<double>("--coverage-threshold");
            var strict = context.ParseResult.GetValueForOption<bool>("--strict");

            try
            {
                var docGenerator = _serviceProvider.GetRequiredService<IDocumentationGenerator>();
                var fileOps = _serviceProvider.GetRequiredService<IFileOperations>();

                Console.WriteLine($"Generating {docType} documentation...");

                // Configure documentation requirements
                var requirements = new DocumentationRequirements
                {
                    MinimumOverallCoverage = coverageThreshold,
                    MinimumTypeCoverage = strict ? 95.0 : 90.0,
                    MinimumMethodCoverage = strict ? 90.0 : 85.0,
                    MinimumPropertyCoverage = strict ? 85.0 : 75.0,
                    RequireExamples = strict,
                    RequireParameterDocs = true,
                    RequireReturnDocs = true,
                    RequireExceptionDocs = strict,
                    MissingDocumentationSeverity = strict ? IssueSeverity.Error : IssueSeverity.Warning
                };

                // Generate documentation based on type
                DocumentationResult result = null;
                switch (docType.ToLower())
                {
                    case "all":
                        result = await docGenerator.GenerateApiDocumentationAsync();
                        if (result.Success)
                        {
                            var pluginResult = await docGenerator.GeneratePluginDocumentationAsync();
                            var validationResult = await docGenerator.GenerateValidationDocumentationAsync();
                            result.Plugins.AddRange(pluginResult.Plugins);
                            result.Validation = validationResult.Validation;
                        }
                        break;

                    case "api":
                        result = await docGenerator.GenerateApiDocumentationAsync();
                        break;

                    case "plugin":
                        result = await docGenerator.GeneratePluginDocumentationAsync();
                        break;

                    case "validation":
                        result = await docGenerator.GenerateValidationDocumentationAsync();
                        break;

                    default:
                        throw new ArgumentException($"Unknown documentation type: {docType}");
                }

                if (result == null || !result.Success)
                {
                    throw new Exception($"Documentation generation failed: {result?.Error ?? "Unknown error"}");
                }

                Console.WriteLine("Documentation generated successfully.");

                // Analyze and report documentation coverage
                var coverageReport = await docGenerator.AnalyzeDocumentationCoverageAsync(requirements);
                if (!coverageReport.MeetsThreshold)
                {
                    Console.WriteLine("\nWarning: Documentation coverage is below required thresholds:");
                    Console.WriteLine($"Overall Coverage: {coverageReport.OverallCoverage:F1}%");
                    Console.WriteLine("\nRecommendations:");
                    foreach (var recommendation in coverageReport.Recommendations)
                    {
                        Console.WriteLine($"- {recommendation}");
                    }
                    Console.WriteLine("\nIssues:");
                    foreach (var issue in coverageReport.Issues.OrderByDescending(i => i.Severity))
                    {
                        Console.WriteLine($"[{issue.Severity}] {issue.Component}: {issue.Description}");
                    }

                    if (strict)
                    {
                        throw new Exception("Documentation coverage does not meet strict requirements");
                    }
                }
                else
                {
                    Console.WriteLine($"\nDocumentation coverage: {coverageReport.OverallCoverage:F1}% (meets requirements)");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        }
    }
}