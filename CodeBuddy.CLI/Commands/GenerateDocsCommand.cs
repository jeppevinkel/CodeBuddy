using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.CLI.Commands
{
    /// <summary>
    /// Command line interface for documentation generation
    /// </summary>
    public class GenerateDocsCommand : Command
    {
        private readonly IDocumentationGenerator _docGenerator;

        public GenerateDocsCommand(IDocumentationGenerator docGenerator)
            : base("docs", "Generate and manage documentation")
        {
            _docGenerator = docGenerator;

            // Add subcommands
            var generateCmd = new Command("generate", "Generate documentation")
            {
                new Option<bool>("--validate", "Validate generated documentation"),
                new Option<bool>("--plugins", "Include plugin documentation"),
                new Option<bool>("--version", "Create new documentation version"),
                new Option<string>("--version-desc", "Version description")
            };
            generateCmd.SetHandler(GenerateHandler);
            AddCommand(generateCmd);

            var validateCmd = new Command("validate", "Validate existing documentation");
            validateCmd.SetHandler(ValidateHandler);
            AddCommand(validateCmd);

            var analyzeCmd = new Command("analyze", "Analyze documentation coverage");
            analyzeCmd.SetHandler(AnalyzeHandler);
            AddCommand(analyzeCmd);

            var crossRefCmd = new Command("cross-ref", "Generate cross-references");
            crossRefCmd.SetHandler(CrossRefHandler);
            AddCommand(crossRefCmd);
        }

        private async Task GenerateHandler(bool validate = true, bool plugins = true, bool version = false, string versionDesc = null)
        {
            try
            {
                Console.WriteLine("Generating documentation...");

                // Generate API documentation
                Console.WriteLine("Generating API documentation...");
                var apiResult = await _docGenerator.GenerateApiDocumentationAsync();
                HandleResult(apiResult);

                if (plugins)
                {
                    Console.WriteLine("Generating plugin documentation...");
                    var pluginResult = await _docGenerator.GeneratePluginDocumentationAsync();
                    HandleResult(pluginResult);
                }

                Console.WriteLine("Generating validation documentation...");
                var validationResult = await _docGenerator.GenerateValidationDocumentationAsync();
                HandleResult(validationResult);

                if (validate)
                {
                    Console.WriteLine("Validating documentation...");
                    var validationResult = await _docGenerator.ValidateDocumentationAsync();
                    if (validationResult.IsValid)
                    {
                        Console.WriteLine($"Documentation validation passed (Quality Score: {validationResult.Coverage:P})");
                    }
                    else
                    {
                        Console.WriteLine("Documentation validation found issues:");
                        foreach (var issue in validationResult.Issues)
                        {
                            Console.WriteLine($"- [{issue.Severity}] {issue.Component}: {issue.Description}");
                        }
                        
                        Console.WriteLine("\nRecommendations:");
                        foreach (var rec in validationResult.Recommendations)
                        {
                            Console.WriteLine($"- {rec}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating documentation: {ex.Message}");
            }
        }

        private async Task ValidateHandler()
        {
            try
            {
                Console.WriteLine("Validating documentation...");
                var result = await _docGenerator.ValidateDocumentationAsync();
                
                if (result.IsValid)
                {
                    Console.WriteLine($"Documentation validation passed (Quality Score: {result.Coverage:P})");
                }
                else
                {
                    Console.WriteLine("Documentation validation found issues:");
                    foreach (var issue in result.Issues)
                    {
                        Console.WriteLine($"- [{issue.Severity}] {issue.Component}: {issue.Description}");
                    }
                }

                if (result.Recommendations.Count > 0)
                {
                    Console.WriteLine("\nRecommendations:");
                    foreach (var rec in result.Recommendations)
                    {
                        Console.WriteLine($"- {rec}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating documentation: {ex.Message}");
            }
        }

        private async Task AnalyzeHandler()
        {
            try
            {
                Console.WriteLine("Analyzing documentation coverage...");
                var result = await _docGenerator.AnalyzeDocumentationCoverageAsync();
                
                Console.WriteLine($"Overall coverage: {result.Coverage:P}");
                Console.WriteLine("\nCoverage by component:");
                foreach (var component in result.ComponentCoverage)
                {
                    Console.WriteLine($"- {component.Key}: {component.Value:P}");
                }

                if (result.MissingDocumentation.Count > 0)
                {
                    Console.WriteLine("\nMissing documentation:");
                    foreach (var item in result.MissingDocumentation)
                    {
                        Console.WriteLine($"- {item}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing documentation: {ex.Message}");
            }
        }

        private async Task CrossRefHandler()
        {
            try
            {
                Console.WriteLine("Generating cross-references...");
                var result = await _docGenerator.GenerateCrossReferencesAsync();
                
                Console.WriteLine($"Generated {result.References.Count} cross-references");
                
                if (result.Issues.Count > 0)
                {
                    Console.WriteLine("\nIssues found:");
                    foreach (var issue in result.Issues)
                    {
                        Console.WriteLine($"- {issue.Component}: {issue.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating cross-references: {ex.Message}");
            }
        }

        private void HandleResult(DocumentationResult result)
        {
            if (result.Success)
            {
                Console.WriteLine("Documentation generated successfully.");
                Console.WriteLine($"Generated {result.Types.Count} type documentations");
                Console.WriteLine($"Generated {result.Plugins.Count} plugin documentations");
                if (result.Validation != null)
                {
                    Console.WriteLine($"Generated validation documentation with {result.Validation.Components.Count} components");
                }
            }
            else
            {
                Console.WriteLine($"Failed to generate documentation: {result.Error}");
            }
        }
    }
}