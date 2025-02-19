using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Core.Implementation.Monitoring;

/// <summary>
/// Manages plugin configuration validation, versioning, and rollback
/// </summary>
public class PluginConfigurationManager
{
    private readonly ILogger<PluginConfigurationManager> _logger;
    private readonly IConfigurationManager _configManager;
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly ConcurrentDictionary<string, List<ConfigurationVersion>> _configHistory;
    private readonly ConcurrentDictionary<string, ConfigurationValidationState> _validationStates;

    public PluginConfigurationManager(
        ILogger<PluginConfigurationManager> logger,
        IConfigurationManager configManager,
        PluginHealthMonitor healthMonitor)
    {
        _logger = logger;
        _configManager = configManager;
        _healthMonitor = healthMonitor;
        _configHistory = new ConcurrentDictionary<string, List<ConfigurationVersion>>();
        _validationStates = new ConcurrentDictionary<string, ConfigurationValidationState>();
    }

    /// <summary>
    /// Records and validates a new configuration version
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateConfiguration(
        string pluginId, 
        string configuration,
        bool applyIfValid = false)
    {
        try
        {
            _logger.LogInformation("Validating configuration for plugin {Id}", pluginId);

            var result = await ValidateConfigurationInternal(pluginId, configuration);

            if (result.IsValid && applyIfValid)
            {
                await ApplyConfiguration(pluginId, configuration);
            }

            // Update validation state
            _validationStates[pluginId] = new ConfigurationValidationState
            {
                LastValidation = DateTime.UtcNow,
                IsValid = result.IsValid,
                ValidationErrors = result.Errors,
                ConfigurationHash = ComputeConfigurationHash(configuration)
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration for plugin {Id}", pluginId);
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = { ex.Message }
            };
        }
    }

    /// <summary>
    /// Detects configuration drift from last known valid state
    /// </summary>
    public async Task<ConfigurationDriftResult> DetectConfigurationDrift(string pluginId)
    {
        try
        {
            var currentConfig = await _configManager.GetConfiguration(pluginId);
            if (currentConfig == null)
            {
                return new ConfigurationDriftResult
                {
                    HasDrift = false,
                    Message = "No configuration found"
                };
            }

            var lastValidState = _validationStates.GetValueOrDefault(pluginId);
            if (lastValidState == null)
            {
                return new ConfigurationDriftResult
                {
                    HasDrift = false,
                    Message = "No previous validation state"
                };
            }

            var currentHash = ComputeConfigurationHash(currentConfig);
            var hasDrift = currentHash != lastValidState.ConfigurationHash;

            return new ConfigurationDriftResult
            {
                HasDrift = hasDrift,
                Message = hasDrift ? "Configuration has changed since last validation" : "No drift detected",
                LastValidHash = lastValidState.ConfigurationHash,
                CurrentHash = currentHash,
                LastValidationTime = lastValidState.LastValidation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting configuration drift for plugin {Id}", pluginId);
            return new ConfigurationDriftResult
            {
                HasDrift = true,
                Message = $"Error detecting drift: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Rolls back to the last known good configuration
    /// </summary>
    public async Task<bool> RollbackConfiguration(string pluginId)
    {
        try
        {
            var versions = _configHistory.GetValueOrDefault(pluginId);
            if (versions == null || !versions.Any())
            {
                _logger.LogWarning("No configuration history available for plugin {Id}", pluginId);
                return false;
            }

            // Find last known good configuration
            var lastGoodVersion = versions
                .Where(v => v.ValidationResult.IsValid)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            if (lastGoodVersion == null)
            {
                _logger.LogWarning("No valid configuration found in history for plugin {Id}", pluginId);
                return false;
            }

            _logger.LogInformation(
                "Rolling back plugin {Id} to configuration from {Time}", 
                pluginId, 
                lastGoodVersion.Timestamp);

            // Apply rollback
            await ApplyConfiguration(pluginId, lastGoodVersion.Configuration);

            // Record rollback
            RecordConfigurationVersion(pluginId, lastGoodVersion.Configuration, "Rollback");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back configuration for plugin {Id}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Records startup/shutdown configuration state
    /// </summary>
    public async Task RecordLifecycleConfiguration(string pluginId, bool isStartup)
    {
        try
        {
            var config = await _configManager.GetConfiguration(pluginId);
            if (config == null) return;

            RecordConfigurationVersion(
                pluginId, 
                config, 
                isStartup ? "Startup" : "Shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error recording {Phase} configuration for plugin {Id}", 
                isStartup ? "startup" : "shutdown",
                pluginId);
        }
    }

    private async Task<ConfigurationValidationResult> ValidateConfigurationInternal(
        string pluginId, 
        string configuration)
    {
        var result = new ConfigurationValidationResult();

        // Basic structure validation
        if (string.IsNullOrWhiteSpace(configuration))
        {
            result.IsValid = false;
            result.Errors.Add("Configuration cannot be empty");
            return result;
        }

        // Schema validation
        try
        {
            // Integration point: Validate against schema
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Schema validation failed: {ex.Message}");
            return result;
        }

        // Dependency validation
        var pluginStatus = _healthMonitor.GetPluginHealth(pluginId);
        if (pluginStatus != null)
        {
            foreach (var depId in pluginStatus.LoadedDependencies)
            {
                var depStatus = _healthMonitor.GetPluginHealth(depId);
                if (depStatus == null || depStatus.State != PluginState.Running)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Dependency {depId} is not in valid state");
                }
            }
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private void RecordConfigurationVersion(string pluginId, string configuration, string reason)
    {
        if (!_configHistory.ContainsKey(pluginId))
        {
            _configHistory[pluginId] = new List<ConfigurationVersion>();
        }

        var version = new ConfigurationVersion
        {
            Timestamp = DateTime.UtcNow,
            Configuration = configuration,
            Reason = reason,
            ConfigurationHash = ComputeConfigurationHash(configuration)
        };

        _configHistory[pluginId].Add(version);

        // Keep only last 10 versions
        if (_configHistory[pluginId].Count > 10)
        {
            _configHistory[pluginId].RemoveAt(0);
        }
    }

    private async Task ApplyConfiguration(string pluginId, string configuration)
    {
        await _configManager.UpdateConfiguration(pluginId, configuration);
        RecordConfigurationVersion(pluginId, configuration, "Update");
    }

    private string ComputeConfigurationHash(string configuration)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(configuration);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class ConfigurationVersion
{
    public DateTime Timestamp { get; set; }
    public string Configuration { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ConfigurationHash { get; set; } = string.Empty;
    public ConfigurationValidationResult ValidationResult { get; set; } = new();
}

public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ConfigurationDriftResult
{
    public bool HasDrift { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LastValidHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
    public DateTime LastValidationTime { get; set; }
}

public class ConfigurationValidationState
{
    public DateTime LastValidation { get; set; }
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public string ConfigurationHash { get; set; } = string.Empty;
}