using CommandLine;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.CLI
{
    public class Program
    {
        [Verb("validate", HelpText = "Validate code against specified rules")]
        public class ValidateOptions
        {
            [Option('p', "path", Required = true, HelpText = "Path to the file or directory to validate")]
            public string Path { get; set; } = string.Empty;

            [Option('r', "rules", Required = false, HelpText = "Comma-separated list of rule IDs to validate against")]
            public string? Rules { get; set; }
        }

        [Verb("list-rules", HelpText = "List available validation rules")]
        public class ListRulesOptions
        {
            [Option('l', "language", Required = false, HelpText = "Filter rules by programming language")]
            public string? Language { get; set; }
        }

        public static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<ValidateOptions, ListRulesOptions>(args)
                .MapResult(
                    async (ValidateOptions opts) => await RunValidateAsync(opts),
                    async (ListRulesOptions opts) => await RunListRulesAsync(opts),
                    errs => Task.FromResult(1));
        }

        private static async Task<int> RunValidateAsync(ValidateOptions opts)
        {
            try
            {
                var services = new ServiceCollection();
                // Add core services here
                var serviceProvider = services.BuildServiceProvider();

                var fileOps = serviceProvider.GetRequiredService<IFileOperations>();
                var ruleManager = serviceProvider.GetRequiredService<IRuleManager>();

                var validationOptions = new ValidationOptions
                {
                    FilePath = opts.Path,
                    RuleIds = opts.Rules?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                };

                var result = await ruleManager.ValidateAsync(validationOptions);
                
                if (result.IsSuccess)
                {
                    Console.WriteLine("Validation successful!");
                    return 0;
                }
                else
                {
                    Console.WriteLine("Validation failed:");
                    foreach (var issue in result.Issues)
                    {
                        Console.WriteLine($"- {issue}");
                    }
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunListRulesAsync(ListRulesOptions opts)
        {
            try
            {
                var services = new ServiceCollection();
                // Add core services here
                var serviceProvider = services.BuildServiceProvider();

                var ruleManager = serviceProvider.GetRequiredService<IRuleManager>();
                var rules = await ruleManager.GetAvailableRulesAsync(opts.Language);

                Console.WriteLine("Available rules:");
                foreach (var rule in rules)
                {
                    Console.WriteLine($"- {rule.Id}: {rule.Description}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}