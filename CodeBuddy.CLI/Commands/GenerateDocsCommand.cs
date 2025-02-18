using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation.Documentation;
using System.CommandLine;
using System.Threading.Tasks;
namespace CodeBuddy.CLI.Commands
{
    /// <summary>
    /// CLI command to generate documentation
    /// </summary>
    public class GenerateDocsCommand : Command
    {
        private readonly DocumentationAPI _docApi;

        public GenerateDocsCommand(DocumentationAPI docApi) 
            : base("docs", "Generate and manage documentation")
        {
            _docApi = docApi;

            // Add subcommands
            AddCommand(new Command("generate", "Generate documentation")
            {
                new Option<bool>("--validate", "Validate generated documentation"),
                new Option<bool>("--plugins", "Include plugin documentation"),
                new Option<bool>("--version", "Create new documentation version"),
                new Option<string>("--version-desc", "Version description")
            });

            AddCommand(new Command("validate", "Validate existing documentation"));
            
            AddCommand(new Command("versions", "List documentation versions"));
            
            AddCommand(new Command("diff", "Show differences between versions")
            {
                new Argument<string>("from", "Source version"),
                new Argument<string>("to", "Target version")
            });

            // Set handlers
            this.SetHandler(DefaultHandler);
        }

        private async Task DefaultHandler()
        {
            await GenerateHandler(true, true, true, null);
        }

        private async Task GenerateHandler(bool validate, bool plugins, bool version, string versionDesc)
        {
            var options = new GenerationOptions
            {
                ValidateDocumentation = validate,
                IncludePlugins = plugins,
                CreateVersion = version,
                VersionDescription = versionDesc
            };

            var result = await _docApi.GenerateDocumentationAsync(options);
            
            if (result.Success)
            {
                Console.WriteLine("Documentation generated successfully");
                if (result.ValidationResult != null)
                {
                    Console.WriteLine($"Documentation quality score: {result.ValidationResult.QualityScore:P}");
                    foreach (var issue in result.ValidationResult.Issues)
                    {
                        Console.WriteLine($"- {issue.Type}: {issue.Message} in {issue.Component}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error generating documentation: {result.Error}");
            }
        }

        private async Task ValidateHandler()
        {
            var result = await _docApi.ValidateDocumentationAsync();
            
            if (result.Success)
            {
                Console.WriteLine($"Documentation quality score: {result.QualityScore:P}");
                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"- {issue.Type}: {issue.Message} in {issue.Component}");
                }
            }
            else
            {
                Console.WriteLine($"Error validating documentation: {result.Error}");
            }
        }

        private async Task ListVersionsHandler()
        {
            var result = await _docApi.GetVersionHistoryAsync();
            
            if (result.Success)
            {
                Console.WriteLine("Documentation versions:");
                foreach (var version in result.Versions)
                {
                    Console.WriteLine($"- {version.Version} ({version.CreatedAt:yyyy-MM-dd HH:mm:ss}): {version.Description}");
                }
            }
            else
            {
                Console.WriteLine($"Error retrieving versions: {result.Error}");
            }
        }

        private async Task DiffVersionsHandler(string fromVersion, string toVersion)
        {
            var changes = await _docApi.GetVersionChangesAsync(fromVersion, toVersion);
            
            Console.WriteLine($"Changes between {fromVersion} and {toVersion}:");
            foreach (var change in changes.ChangedFiles)
            {
                var changeType = change.ChangeType.ToString().ToUpper();
                Console.WriteLine($"[{changeType}] {change.File}");
            }
        }
    }/// <summary>
    /// Command line interface for documentation generation
    /// </summary>
    public class GenerateDocsCommand : Command
    {
        private readonly IDocumentationGenerator _docGenerator;

        public GenerateDocsCommand(IDocumentationGenerator docGenerator)
            : base("generate-docs", "Generate documentation for the codebase")
        {
            _docGenerator = docGenerator;

            var typeOption = new Option<string>(
                "--type",
                "Type of documentation to generate (api, plugin, validation, all)");
            AddOption(typeOption);

            this.SetHandler(HandleCommand, typeOption);
        }

        private async Task HandleCommand(string type)
        {
            try
            {
                Console.WriteLine($"Generating {type} documentation...");

                switch (type.ToLower())
                {
                    case "api":
                        var apiResult = await _docGenerator.GenerateApiDocumentationAsync();
                        HandleResult(apiResult);
                        break;

                    case "plugin":
                        var pluginResult = await _docGenerator.GeneratePluginDocumentationAsync();
                        HandleResult(pluginResult);
                        break;

                    case "validation":
                        var validationResult = await _docGenerator.GenerateValidationDocumentationAsync();
                        HandleResult(validationResult);
                        break;

                    case "all":
                        var apiRes = await _docGenerator.GenerateApiDocumentationAsync();
                        var pluginRes = await _docGenerator.GeneratePluginDocumentationAsync();
                        var validationRes = await _docGenerator.GenerateValidationDocumentationAsync();

                        HandleResult(apiRes);
                        HandleResult(pluginRes);
                        HandleResult(validationRes);
                        break;

                    default:
                        Console.WriteLine($"Unknown documentation type: {type}");
                        Console.WriteLine("Valid types: api, plugin, validation, all");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating documentation: {ex.Message}");
            }
        }

        private void HandleResult(Core.Models.Documentation.DocumentationResult result)
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