using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    public class GenerateCodeCommand : Command
    {
        private readonly ICodeGenerator _codeGenerator;

        public GenerateCodeCommand(ICodeGenerator codeGenerator)
            : base("generate", "Generate code based on templates and patterns")
        {
            _codeGenerator = codeGenerator;

            var templateOption = new Option<string>(
                "--template",
                "Template to use for code generation");
            AddOption(templateOption);

            var outputOption = new Option<string>(
                "--output",
                "Output directory for generated code");
            AddOption(outputOption);

            var nameOption = new Option<string>(
                "--name",
                "Name for the generated code component");
            AddOption(nameOption);

            this.SetHandler(HandleCommand, templateOption, outputOption, nameOption);
        }

        private async Task HandleCommand(string template, string output, string name)
        {
            try
            {
                Console.WriteLine($"Generating code using template '{template}'...");
                
                var result = await _codeGenerator.GenerateAsync(new Core.Models.CodeSnippet
                {
                    TemplateName = template,
                    OutputPath = output,
                    ComponentName = name
                });

                if (result.Success)
                {
                    Console.WriteLine($"Code generated successfully at: {result.OutputPath}");
                    foreach (var file in result.GeneratedFiles)
                    {
                        Console.WriteLine($"- {file}");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to generate code: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating code: {ex.Message}");
            }
        }
    }
}