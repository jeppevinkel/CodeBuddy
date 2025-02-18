using System;
using System.CommandLine;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    public class ConfigCommand : Command
    {
        private readonly IConfigurationManager _configManager;

        public ConfigCommand(IConfigurationManager configManager)
            : base("config", "Manage CodeBuddy configuration")
        {
            _configManager = configManager;

            // Get config value
            var getCommand = new Command("get", "Get configuration value");
            var getKeyOption = new Option<string>("--key", "Configuration key to get");
            getCommand.AddOption(getKeyOption);
            getCommand.SetHandler(HandleGet, getKeyOption);
            AddCommand(getCommand);

            // Set config value
            var setCommand = new Command("set", "Set configuration value");
            var setKeyOption = new Option<string>("--key", "Configuration key to set");
            var valueOption = new Option<string>("--value", "Value to set");
            setCommand.AddOption(setKeyOption);
            setCommand.AddOption(valueOption);
            setCommand.SetHandler(HandleSet, setKeyOption, valueOption);
            AddCommand(setCommand);

            // List all config
            var listCommand = new Command("list", "List all configuration values");
            listCommand.SetHandler(HandleList);
            AddCommand(listCommand);

            // Reset config
            var resetCommand = new Command("reset", "Reset configuration to defaults");
            resetCommand.SetHandler(HandleReset);
            AddCommand(resetCommand);
        }

        private async Task HandleGet(string key)
        {
            try
            {
                var value = await _configManager.GetConfigValueAsync(key);
                if (value != null)
                {
                    Console.WriteLine($"{key}={value}");
                }
                else
                {
                    Console.WriteLine($"Configuration key '{key}' not found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting configuration: {ex.Message}");
            }
        }

        private async Task HandleSet(string key, string value)
        {
            try
            {
                Console.WriteLine($"Setting {key}={value}...");
                
                var result = await _configManager.SetConfigValueAsync(key, value);
                
                if (result.Success)
                {
                    Console.WriteLine("Configuration updated successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to update configuration: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting configuration: {ex.Message}");
            }
        }

        private async Task HandleList()
        {
            try
            {
                var config = await _configManager.GetConfigurationAsync();
                
                Console.WriteLine("Current configuration:");
                foreach (var item in config)
                {
                    Console.WriteLine($"{item.Key}={item.Value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing configuration: {ex.Message}");
            }
        }

        private async Task HandleReset()
        {
            try
            {
                Console.WriteLine("Resetting configuration to defaults...");
                
                var result = await _configManager.ResetToDefaultsAsync();
                
                if (result.Success)
                {
                    Console.WriteLine("Configuration reset successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to reset configuration: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting configuration: {ex.Message}");
            }
        }
    }
}