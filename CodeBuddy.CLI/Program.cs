using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Extensions;
using CodeBuddy.CLI.Commands;

namespace CodeBuddy.CLI
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                var services = new ServiceCollection();
                services.AddCodeBuddyServices();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddFile("logs/codebuddy-cli.log");
                });
                
                var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                
                var rootCommand = new RootCommand("CodeBuddy CLI tool for code analysis and generation");

                // Add all available commands
                rootCommand.AddCommand(new GenerateDocsCommand(serviceProvider.GetRequiredService<IDocumentationGenerator>()));
                rootCommand.AddCommand(new GenerateCodeCommand(serviceProvider.GetRequiredService<ICodeGenerator>()));
                rootCommand.AddCommand(new TemplateCommand(serviceProvider.GetRequiredService<ITemplateManager>()));
                rootCommand.AddCommand(new ConfigCommand(serviceProvider.GetRequiredService<IConfigurationManager>()));
                rootCommand.AddCommand(new GitCommand(serviceProvider.GetRequiredService<IFileOperations>()));

                // Set up global options
                var verboseOption = new Option<bool>("--verbose", "Enable verbose output");
                rootCommand.AddGlobalOption(verboseOption);

                try
                {
                    return await rootCommand.InvokeAsync(args);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Command execution failed");
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Critical error: {ex.Message}");
                return 1;
            }
        }
    }
}