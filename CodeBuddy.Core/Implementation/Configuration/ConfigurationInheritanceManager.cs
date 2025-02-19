using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration inheritance and overrides with priority levels
    /// </summary>
    public class ConfigurationInheritanceManager
    {
        private readonly ILogger _logger;
        private readonly IConfigurationManager _configManager;
        private readonly Dictionary<string, List<ConfigurationOverride>> _overrides;
        private readonly Dictionary<string, ConfigurationPriority> _priorities;

        public ConfigurationInheritanceManager(
            ILogger logger,
            IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _overrides = new Dictionary<string, List<ConfigurationOverride>>();
            _priorities = new Dictionary<string, ConfigurationPriority>();
        }

        public async Task<T> GetInheritedConfigurationAsync<T>(string section, string userId = "") 
            where T : BaseConfiguration, new()
        {
            // Get base configuration
            var baseConfig = await _configManager.GetConfigurationAsync<T>(section);

            // Apply global overrides
            var config = ApplyOverrides(baseConfig, section, ConfigurationScope.Global);

            // Apply environment-specific overrides
            if (!string.IsNullOrEmpty(config.Environment))
            {
                config = ApplyOverrides(config, section, ConfigurationScope.Environment, config.Environment);
            }

            // Apply user-specific overrides
            if (!string.IsNullOrEmpty(userId))
            {
                config = ApplyOverrides(config, section, ConfigurationScope.User, userId);
            }

            return config;
        }

        public void RegisterOverride<T>(
            string section,
            ConfigurationScope scope,
            string scopeId,
            Action<T> override_,
            int priority = 0) where T : BaseConfiguration
        {
            var key = GetOverrideKey(section, scope, scopeId);
            
            if (!_overrides.ContainsKey(key))
            {
                _overrides[key] = new List<ConfigurationOverride>();
            }

            _overrides[key].Add(new ConfigurationOverride
            {
                Priority = priority,
                Apply = config => override_((T)config)
            });

            // Sort overrides by priority
            _overrides[key].Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void SetPriority(string section, ConfigurationPriority priority)
        {
            _priorities[section] = priority;
        }

        private T ApplyOverrides<T>(
            T config,
            string section,
            ConfigurationScope scope,
            string scopeId = "") where T : BaseConfiguration
        {
            var key = GetOverrideKey(section, scope, scopeId);
            
            if (!_overrides.ContainsKey(key))
            {
                return config;
            }

            var priority = _priorities.GetValueOrDefault(section, ConfigurationPriority.Default);
            var configCopy = DeepClone(config);

            foreach (var override_ in _overrides[key].Where(o => IsOverrideAllowed(o, priority)))
            {
                try
                {
                    override_.Apply(configCopy);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying configuration override for section {Section}", section);
                }
            }

            return configCopy;
        }

        private string GetOverrideKey(string section, ConfigurationScope scope, string scopeId)
        {
            return $"{section}:{scope}:{scopeId}";
        }

        private bool IsOverrideAllowed(ConfigurationOverride override_, ConfigurationPriority priority)
        {
            return priority switch
            {
                ConfigurationPriority.Strict => override_.Priority >= 100,
                ConfigurationPriority.High => override_.Priority >= 50,
                ConfigurationPriority.Default => true,
                _ => true
            };
        }

        private T DeepClone<T>(T obj) where T : BaseConfiguration
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
        }
    }

    public class ConfigurationOverride
    {
        public int Priority { get; set; }
        public Action<object> Apply { get; set; } = _ => { };
    }

    public enum ConfigurationScope
    {
        Global,
        Environment,
        User
    }

    public enum ConfigurationPriority
    {
        Default,
        High,
        Strict
    }
}