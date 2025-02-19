using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Specialized configuration manager for plugin configurations with stability controls
    /// </summary>
    public class PluginConfigurationManager
    {
        private readonly ILogger _logger;
        private readonly IConfigurationManager _configManager;
        private readonly ConcurrentDictionary<string, object> _pluginDefaults;
        private readonly ConcurrentDictionary<string, ConfigurationStabilitySettings> _stabilitySettings;

        public PluginConfigurationManager(
            ILogger logger,
            IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _pluginDefaults = new ConcurrentDictionary<string, object>();
            _stabilitySettings = new ConcurrentDictionary<string, ConfigurationStabilitySettings>();
        }

        public async Task<T> GetPluginConfigurationAsync<T>(string pluginId) where T : PluginConfiguration, new()
        {
            // Get base configuration
            var config = await _configManager.GetConfigurationAsync<T>($"plugins/{pluginId}");

            // Apply stability settings
            var settings = _stabilitySettings.GetOrAdd(pluginId, new ConfigurationStabilitySettings());
            config.StabilitySettings = settings;

            // Register for changes with stability checks
            _configManager.RegisterConfigurationChangeCallback<T>($"plugins/{pluginId}", newConfig =>
            {
                HandlePluginConfigurationChange(pluginId, newConfig);
            });

            return config;
        }

        public void SetPluginDefaults<T>(string pluginId, T defaults) where T : PluginConfiguration
        {
            _pluginDefaults[pluginId] = defaults;
        }

        public void ConfigureStabilitySettings(string pluginId, Action<ConfigurationStabilitySettings> configure)
        {
            var settings = _stabilitySettings.GetOrAdd(pluginId, new ConfigurationStabilitySettings());
            configure(settings);
        }

        private void HandlePluginConfigurationChange<T>(string pluginId, T newConfig) where T : PluginConfiguration
        {
            var settings = _stabilitySettings.GetOrAdd(pluginId, new ConfigurationStabilitySettings());

            if (settings.RequireStabilityPeriod)
            {
                // Ensure configuration has been stable for required period
                if (!HasMetStabilityPeriod(pluginId))
                {
                    _logger.LogWarning("Plugin {PluginId} configuration change rejected: stability period not met", pluginId);
                    return;
                }
            }

            if (settings.ValidateResourceLimits)
            {
                // Validate resource limits
                if (!AreResourceLimitsValid(newConfig))
                {
                    _logger.LogWarning("Plugin {PluginId} configuration change rejected: resource limits exceeded", pluginId);
                    return;
                }
            }

            if (settings.RequireGracefulTransition)
            {
                // Schedule graceful transition
                ScheduleGracefulTransition(pluginId, newConfig);
            }
        }

        private bool HasMetStabilityPeriod(string pluginId)
        {
            var settings = _stabilitySettings.GetOrAdd(pluginId, new ConfigurationStabilitySettings());
            var lastChange = settings.LastChangeTimestamp;

            return (DateTime.UtcNow - lastChange) >= settings.StabilityPeriod;
        }

        private bool AreResourceLimitsValid<T>(T config) where T : PluginConfiguration
        {
            // Validate memory limits
            if (config.MaxMemoryMB > 0 && config.MaxMemoryMB > GetSystemMaxMemory())
            {
                return false;
            }

            // Validate CPU limits
            if (config.MaxCPUPercent > 0 && config.MaxCPUPercent > 100)
            {
                return false;
            }

            // Validate concurrent operations
            if (config.MaxConcurrentOperations > 0 && config.MaxConcurrentOperations > GetSystemMaxConcurrency())
            {
                return false;
            }

            return true;
        }

        private void ScheduleGracefulTransition<T>(string pluginId, T newConfig) where T : PluginConfiguration
        {
            // Store transition request
            var settings = _stabilitySettings.GetOrAdd(pluginId, new ConfigurationStabilitySettings());
            settings.PendingTransition = new ConfigurationTransition
            {
                ScheduledTime = DateTime.UtcNow + settings.TransitionDelay,
                NewConfiguration = newConfig
            };

            _logger.LogInformation(
                "Scheduled graceful configuration transition for plugin {PluginId} at {TransitionTime}",
                pluginId,
                settings.PendingTransition.ScheduledTime);
        }

        private int GetSystemMaxMemory()
        {
            // Return system maximum available memory in MB
            return Environment.SystemPageSize * Environment.ProcessorCount / (1024 * 1024);
        }

        private int GetSystemMaxConcurrency()
        {
            // Return system maximum recommended concurrency
            return Environment.ProcessorCount * 2;
        }
    }

    public class ConfigurationStabilitySettings
    {
        public bool RequireStabilityPeriod { get; set; }
        public TimeSpan StabilityPeriod { get; set; } = TimeSpan.FromMinutes(5);
        public DateTime LastChangeTimestamp { get; set; } = DateTime.UtcNow;
        public bool ValidateResourceLimits { get; set; } = true;
        public bool RequireGracefulTransition { get; set; }
        public TimeSpan TransitionDelay { get; set; } = TimeSpan.FromMinutes(1);
        public ConfigurationTransition? PendingTransition { get; set; }
    }

    public class ConfigurationTransition
    {
        public DateTime ScheduledTime { get; set; }
        public object? NewConfiguration { get; set; }
    }
}