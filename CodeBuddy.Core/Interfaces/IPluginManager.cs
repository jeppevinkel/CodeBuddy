using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Marketplace;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages plugin lifecycle, operations, and marketplace integration with hot-reload capabilities
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Loads plugins from the specified directory
    /// </summary>
    Task<IEnumerable<IPlugin>> LoadPluginsAsync(string directory);
    
    /// <summary>
    /// Enables monitoring of the specified plugin directory for changes
    /// </summary>
    Task StartPluginMonitoringAsync(string directory);
    
    /// <summary>
    /// Stops monitoring the plugin directory
    /// </summary>
    Task StopPluginMonitoringAsync();
    
    /// <summary>
    /// Hot-reloads a specific plugin
    /// </summary>
    Task<bool> ReloadPluginAsync(string pluginId);
    
    /// <summary>
    /// Enables a plugin
    /// </summary>
    Task<bool> EnablePluginAsync(string pluginId);
    
    /// <summary>
    /// Disables a plugin
    /// </summary>
    Task<bool> DisablePluginAsync(string pluginId);
    
    /// <summary>
    /// Gets all currently enabled plugins
    /// </summary>
    IEnumerable<IPlugin> GetEnabledPlugins();
    
    /// <summary>
    /// Gets the health status of a specific plugin
    /// </summary>
    Task<PluginHealthStatus> GetPluginHealthAsync(string pluginId);
    
    /// <summary>
    /// Gets the health status of all plugins
    /// </summary>
    Task<IEnumerable<PluginHealthStatus>> GetAllPluginsHealthAsync();

    /// <summary>
    /// Gets a specific plugin by its ID
    /// </summary>
    Task<IPlugin> GetPluginAsync(string pluginId);

    /// <summary>
    /// Installs a plugin from a package byte array
    /// </summary>
    Task<IPlugin> InstallPluginAsync(byte[] packageData);

    /// <summary>
    /// Uninstalls a plugin
    /// </summary>
    Task<bool> UninstallPluginAsync(string pluginId);

    /// <summary>
    /// Checks for plugin updates
    /// </summary>
    Task<IEnumerable<PluginUpdateInfo>> CheckForUpdatesAsync();

    /// <summary>
    /// Gets plugin dependencies
    /// </summary>
    Task<IEnumerable<PluginDependency>> GetPluginDependenciesAsync(string pluginId);
}