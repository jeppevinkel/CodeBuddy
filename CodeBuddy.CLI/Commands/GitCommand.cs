using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    public class GitCommand : Command
    {
        private readonly IFileOperations _fileOps;

        public GitCommand(IFileOperations fileOps)
            : base("git", "Git operations helpers")
        {
            _fileOps = fileOps;

            // Initialize repository command
            var initCommand = new Command("init", "Initialize a new Git repository with CodeBuddy templates");
            var nameOption = new Option<string>("--name", "Project name");
            var typeOption = new Option<string>("--type", "Project type (e.g., csharp, python, javascript)");
            initCommand.AddOption(nameOption);
            initCommand.AddOption(typeOption);
            initCommand.SetHandler(HandleInit, nameOption, typeOption);
            AddCommand(initCommand);

            // Add CodeBuddy files command
            var addCommand = new Command("add-files", "Add CodeBuddy configuration files to existing repository");
            addCommand.SetHandler(HandleAddFiles);
            AddCommand(addCommand);

            // Update gitignore command
            var updateGitignoreCommand = new Command("update-gitignore", "Update .gitignore with CodeBuddy patterns");
            updateGitignoreCommand.SetHandler(HandleUpdateGitignore);
            AddCommand(updateGitignoreCommand);
        }

        private async Task HandleInit(string name, string type)
        {
            try
            {
                Console.WriteLine($"Initializing new {type} project '{name}'...");
                
                var result = await _fileOps.InitializeProjectAsync(new Core.Models.ProjectStructure
                {
                    Name = name,
                    Type = type
                });

                if (result.Success)
                {
                    Console.WriteLine("Project initialized successfully.");
                    Console.WriteLine("Files created:");
                    foreach (var file in result.CreatedFiles)
                    {
                        Console.WriteLine($"- {file}");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to initialize project: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing project: {ex.Message}");
            }
        }

        private async Task HandleAddFiles()
        {
            try
            {
                Console.WriteLine("Adding CodeBuddy files to repository...");
                
                var result = await _fileOps.AddCodeBuddyFilesAsync();

                if (result.Success)
                {
                    Console.WriteLine("Files added successfully:");
                    foreach (var file in result.AddedFiles)
                    {
                        Console.WriteLine($"- {file}");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to add files: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding files: {ex.Message}");
            }
        }

        private async Task HandleUpdateGitignore()
        {
            try
            {
                Console.WriteLine("Updating .gitignore...");
                
                var result = await _fileOps.UpdateGitignoreAsync();

                if (result.Success)
                {
                    Console.WriteLine(".gitignore updated successfully");
                    if (result.AddedPatterns?.Count > 0)
                    {
                        Console.WriteLine("Added patterns:");
                        foreach (var pattern in result.AddedPatterns)
                        {
                            Console.WriteLine($"- {pattern}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to update .gitignore: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating .gitignore: {ex.Message}");
            }
        }
    }
}