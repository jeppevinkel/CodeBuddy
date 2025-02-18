using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Base interface for plugins with hot-reload support
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier of the plugin
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Display name of the plugin
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Plugin version string
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Plugin configuration management
    /// </summary>
    IPluginConfiguration Configuration { get; }
    
    /// <summary>
    /// Plugin state management
    /// </summary>
    IPluginState State { get; }
    
    /// <summary>
    /// Plugin dependencies
    /// </summary>
    IEnumerable<IPluginDependency> Dependencies { get; }
    
    /// <summary>
    /// Initializes the plugin
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="context">Initialization context data</param>
    Task InitializeAsync(ILogger logger, JsonObject? context = null);
    
    /// <summary>
    /// Shuts down the plugin and performs cleanup
    /// </summary>
    Task ShutdownAsync();
    
    /// <summary>
    /// Validates that all plugin dependencies are satisfied
    /// </summary>
    /// <param name="availablePlugins">Collection of available plugins</param>
    /// <returns>True if all dependencies are satisfied</returns>
    bool ValidateDependencies(IEnumerable<IPlugin> availablePlugins);
    
    /// <summary>
    /// Handles plugin errors and performs recovery if possible
    /// </summary>
    /// <param name="error">The error that occurred</param>
    /// <returns>True if recovery was successful</returns>
    Task<bool> HandleErrorAsync(Exception error);
    
    /// <summary>
    /// Prepares the plugin for hot-reload by cleaning up resources
    /// </summary>
    Task PrepareForReloadAsync();
    
    /// <summary>
    /// Migrates plugin state from a previous version during hot-reload
    /// </summary>
    /// <param name="previousState">State from the previous version</param>
    Task MigrateStateAsync(JsonObject? previousState);
    
    /// <summary>
    /// Validates plugin's health and configuration
    /// </summary>
    /// <returns>True if the plugin is healthy and properly configured</returns>
    Task<bool> ValidateHealthAsync();
    
    /// <summary>
    /// Gets the current resource usage statistics
    /// </summary>
    /// <returns>Resource usage metrics</returns>
    Task<ResourceMetrics> GetResourceMetricsAsync();
}