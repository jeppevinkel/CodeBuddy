using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Validates configuration transitions to ensure integrity and consistency
    /// </summary>
    public class ConfigurationTransitionValidator
    {
        private readonly Dictionary<string, Type> _configurationSchema;
        private readonly Dictionary<string, List<string>> _dependencyGraph;
        private readonly HashSet<string> _immutableSettings;

        public ConfigurationTransitionValidator(
            Dictionary<string, Type> configurationSchema,
            Dictionary<string, List<string>> dependencyGraph,
            HashSet<string> immutableSettings)
        {
            _configurationSchema = configurationSchema;
            _dependencyGraph = dependencyGraph;
            _immutableSettings = immutableSettings;
        }

        public (bool IsValid, List<string> ValidationErrors) ValidateTransition(
            Dictionary<string, object> currentConfig,
            Dictionary<string, object> newConfig,
            string migrationId = null)
        {
            var errors = new List<string>();

            // Check type consistency
            foreach (var (key, value) in newConfig)
            {
                if (_configurationSchema.TryGetValue(key, out var expectedType))
                {
                    if (value != null && !expectedType.IsInstanceOfType(value))
                    {
                        errors.Add($"Invalid type for {key}. Expected {expectedType.Name} but got {value.GetType().Name}");
                    }
                }
            }

            // Check immutable settings
            if (migrationId == null) // Only check for manual changes
            {
                foreach (var setting in _immutableSettings)
                {
                    if (newConfig.ContainsKey(setting) && currentConfig.ContainsKey(setting) &&
                        !Equals(newConfig[setting], currentConfig[setting]))
                    {
                        errors.Add($"Setting {setting} is immutable and cannot be changed manually");
                    }
                }
            }

            // Check dependency constraints
            foreach (var (key, dependencies) in _dependencyGraph)
            {
                if (newConfig.ContainsKey(key))
                {
                    foreach (var dependency in dependencies)
                    {
                        if (!newConfig.ContainsKey(dependency) && 
                            (!currentConfig.ContainsKey(dependency) || currentConfig[dependency] == null))
                        {
                            errors.Add($"Setting {key} requires {dependency} to be configured");
                        }
                    }
                }
            }

            return (errors.Count == 0, errors);
        }

        public (bool IsValid, List<string>) ValidateReferentialIntegrity(
            Dictionary<string, object> config)
        {
            var errors = new List<string>();

            // Validate all dependencies are satisfied
            foreach (var (key, dependencies) in _dependencyGraph)
            {
                if (config.ContainsKey(key) && config[key] != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        if (!config.ContainsKey(dependency) || config[dependency] == null)
                        {
                            errors.Add($"Missing required dependency {dependency} for {key}");
                        }
                    }
                }
            }

            return (errors.Count == 0, errors);
        }
    }
}