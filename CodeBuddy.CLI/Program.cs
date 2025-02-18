using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Extensions;
using CodeBuddy.CLI.Commands;

namespace CodeBuddy.CLI
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddCodeBuddyServices();
            
            var serviceProvider = services.BuildServiceProvider();
            
            var rootCommand = new RootCommand("CodeBuddy CLI tool");
            rootCommand.AddCommand(new GenerateDocsCommand(serviceProvider.GetRequiredService<IDocumentationGenerator>()));

            return await rootCommand.InvokeAsync(args);
        }
    }
}