using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    /// <summary>
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