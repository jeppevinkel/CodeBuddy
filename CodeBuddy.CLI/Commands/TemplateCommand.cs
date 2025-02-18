using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    public class TemplateCommand : Command
    {
        private readonly ITemplateManager _templateManager;

        public TemplateCommand(ITemplateManager templateManager)
            : base("template", "Manage code generation templates")
        {
            _templateManager = templateManager;

            // List templates subcommand
            var listCommand = new Command("list", "List all available templates");
            listCommand.SetHandler(HandleList);
            AddCommand(listCommand);

            // Add template subcommand
            var addCommand = new Command("add", "Add a new template");
            var nameOption = new Option<string>("--name", "Template name");
            var pathOption = new Option<string>("--path", "Path to template file");
            addCommand.AddOption(nameOption);
            addCommand.AddOption(pathOption);
            addCommand.SetHandler(HandleAdd, nameOption, pathOption);
            AddCommand(addCommand);

            // Remove template subcommand
            var removeCommand = new Command("remove", "Remove a template");
            var templateOption = new Option<string>("--name", "Template name to remove");
            removeCommand.AddOption(templateOption);
            removeCommand.SetHandler(HandleRemove, templateOption);
            AddCommand(removeCommand);
        }

        private async Task HandleList()
        {
            try
            {
                var templates = await _templateManager.GetTemplatesAsync();
                
                Console.WriteLine("Available templates:");
                foreach (var template in templates)
                {
                    Console.WriteLine($"- {template.Name}: {template.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing templates: {ex.Message}");
            }
        }

        private async Task HandleAdd(string name, string path)
        {
            try
            {
                Console.WriteLine($"Adding template '{name}' from {path}...");
                
                var result = await _templateManager.AddTemplateAsync(new Core.Models.Template
                {
                    Name = name,
                    Path = path
                });

                if (result.Success)
                {
                    Console.WriteLine("Template added successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to add template: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding template: {ex.Message}");
            }
        }

        private async Task HandleRemove(string name)
        {
            try
            {
                Console.WriteLine($"Removing template '{name}'...");
                
                var result = await _templateManager.RemoveTemplateAsync(name);
                
                if (result.Success)
                {
                    Console.WriteLine("Template removed successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to remove template: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing template: {ex.Message}");
            }
        }
    }
}