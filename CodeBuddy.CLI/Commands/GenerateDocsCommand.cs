using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation.Documentation;

namespace CodeBuddy.CLI.Commands
{
    public class GenerateDocsCommand : Command
    {
        private readonly IDocumentationGenerator _docGenerator;
        private readonly Option<bool> _includeTypescript;
        private readonly Option<bool> _includeDiagrams;
        private readonly Option<bool> _validate;
        private readonly Option<bool> _createVersion;
        private readonly Option<string> _outputPath;
        private readonly Option<string> _version;
        private readonly Option<string> _versionDescription;
        private readonly Option<int> _minCoverage;

        public GenerateDocsCommand(IDocumentationGenerator docGenerator) : base(
            name: "generate-docs",
            description: "Generate comprehensive documentation")
        {
            _docGenerator = docGenerator;

            _includeTypescript = new Option<bool>(
                aliases: new[] { "--include-typescript", "-t" },
                description: "Generate TypeScript definitions",
                getDefaultValue: () => true);

            _includeDiagrams = new Option<bool>(
                aliases: new[] { "--include-diagrams", "-d" },
                description: "Generate architecture diagrams",
                getDefaultValue: () => true);

            _validate = new Option<bool>(
                aliases: new[] { "--validate", "-v" },
                description: "Validate documentation",
                getDefaultValue: () => true);

            _createVersion = new Option<bool>(
                aliases: new[] { "--create-version" },
                description: "Create a new documentation version",
                getDefaultValue: () => true);

            _outputPath = new Option<string>(
                aliases: new[] { "--output-path", "-o" },
                description: "Output directory for documentation",
                getDefaultValue: () => "./docs");

            _version = new Option<string>(
                aliases: new[] { "--version" },
                description: "Documentation version");

            _versionDescription = new Option<string>(
                aliases: new[] { "--version-description" },
                description: "Documentation version description");

            _minCoverage = new Option<int>(
                aliases: new[] { "--min-coverage", "-c" },
                description: "Minimum documentation coverage percentage",
                getDefaultValue: () => 80);

            AddOption(_includeTypescript);
            AddOption(_includeDiagrams);
            AddOption(_validate);
            AddOption(_createVersion);
            AddOption(_outputPath);
            AddOption(_version);
            AddOption(_versionDescription);
            AddOption(_minCoverage);

            this.SetHandler(HandleCommand);
        }

        private async Task HandleCommand(InvocationContext context)
        {
            try
            {
                var options = new GenerationOptions
                {
                    GenerateTypeScriptTypes = context.ParseResult.GetValueForOption(_includeTypescript),
                    GenerateDiagrams = context.ParseResult.GetValueForOption(_includeDiagrams),
                    ValidateDocumentation = context.ParseResult.GetValueForOption(_validate),
                    CreateVersion = context.ParseResult.GetValueForOption(_createVersion),
                    Version = context.ParseResult.GetValueForOption(_version),
                    VersionDescription = context.ParseResult.GetValueForOption(_versionDescription),
                    ValidationOptions = new ValidationOptions
                    {
                        MinimumDocumentationCoverage = context.ParseResult.GetValueForOption(_minCoverage)
                    }
                };

                Console.WriteLine("Generating documentation...");
                
                var documentationApi = new DocumentationAPI(_docGenerator, 
                    new DocumentationValidator(), 
                    new DocumentationVersionManager(new FileOperations()));

                var result = await documentationApi.GenerateDocumentationAsync(options);

                if (result.Success)
                {
                    Console.WriteLine("Documentation generated successfully");
                    
                    if (result.ValidationResult != null)
                    {
                        Console.WriteLine($"Documentation coverage: {result.ValidationResult.Coverage}%");
                        foreach (var issue in result.ValidationResult.Issues)
                        {
                            Console.WriteLine($"- {issue.Severity}: {issue.Description}");
                        }
                    }

                    if (result.Version != null)
                    {
                        Console.WriteLine($"Created documentation version: {result.Version.Version}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error generating documentation: {result.Error}");
                    context.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        }
    }
}