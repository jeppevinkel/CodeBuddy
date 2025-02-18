using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.Documentation;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.CLI.Commands
{
    public class ValidateDocsCommand : Command
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidateDocsCommand(IServiceProvider serviceProvider) 
            : base("validate-docs", "Validates documentation coverage and quality")
        {
            _serviceProvider = serviceProvider;

            var thresholdOption = new Option<double>(
                "--min-coverage",
                getDefaultValue: () => 0.8,
                description: "Minimum required documentation coverage"
            );

            var reportPathOption = new Option<string>(
                "--report-path",
                getDefaultValue: () => "docs/reports",
                description: "Path to output documentation reports"
            );

            AddOption(thresholdOption);
            AddOption(reportPathOption);

            this.SetHandler(ExecuteAsync);
        }

        private async Task<int> ExecuteAsync(InvocationContext context)
        {
            try
            {
                var docGenerator = _serviceProvider.GetRequiredService<IDocumentationGenerator>();
                var validationResult = await docGenerator.ValidateDocumentationAsync();

                var pluginValidator = _serviceProvider.GetRequiredService<PluginDocumentationValidator>();
                var pluginValidationResult = await pluginValidator.ValidatePluginDocumentationAsync();

                var reportGenerator = _serviceProvider.GetRequiredService<DocumentationReportGenerator>();
                await reportGenerator.GenerateReportAsync(validationResult, pluginValidationResult);

                var badgeGenerator = _serviceProvider.GetRequiredService<DocumentationBadgeGenerator>();
                await badgeGenerator.GenerateCoverageBadgesAsync(validationResult.Coverage);

                // Output results to console
                Console.WriteLine($"Documentation Coverage: {validationResult.Coverage.PublicApiCoverage:P0}");
                Console.WriteLine($"Parameter Documentation: {validationResult.Coverage.ParameterDocumentationCoverage:P0}");
                Console.WriteLine($"Code Examples: {validationResult.Coverage.CodeExampleCoverage:P0}");
                Console.WriteLine($"Interface Documentation: {validationResult.Coverage.InterfaceImplementationCoverage:P0}");
                Console.WriteLine($"Plugin Documentation: {pluginValidationResult.OverallCompleteness:P0}");

                // Output warnings
                foreach (var issue in validationResult.Issues)
                {
                    Console.WriteLine($"Warning: {issue.Description}");
                }

                return validationResult.IsValid ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error validating documentation: {ex.Message}");
                return 1;
            }
        }
    }
}