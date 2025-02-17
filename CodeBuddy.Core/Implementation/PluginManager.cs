using System.Reflection;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements plugin management with dynamic loading and lifecycle management
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly ILogger _pluginLogger;
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly HashSet<string> _enabledPlugins = new();

    public PluginManager(ILogger<PluginManager> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _pluginLogger = loggerFactory.CreateLogger("Plugins");
    }

    public async Task<IEnumerable<IPlugin>> LoadPluginsAsync(string directory)
    {
        try
        {
            _logger.LogInformation("Loading plugins from directory {Directory}", directory);

            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
                return Enumerable.Empty<IPlugin>();
            }

            var pluginFiles = Directory.GetFiles(directory, "*.dll");
            var loadedPlugins = new List<IPlugin>();

            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(pluginFile);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var pluginType in pluginTypes)
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                        await plugin.InitializeAsync(_pluginLogger);
                        
                        _plugins[plugin.Id] = plugin;
                        loadedPlugins.Add(plugin);
                        
                        _logger.LogInformation("Loaded plugin: {Id} ({Name} v{Version})", 
                            plugin.Id, plugin.Name, plugin.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading plugin from {File}", pluginFile);
                }
            }

            return loadedPlugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins from directory {Directory}", directory);
            throw;
        }
    }

    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                _logger.LogWarning("Plugin not found: {Id}", pluginId);
                return false;
            }

            if (_enabledPlugins.Contains(pluginId))
            {
                _logger.LogInformation("Plugin already enabled: {Id}", pluginId);
                return true;
            }

            await plugin.InitializeAsync(_pluginLogger);
            _enabledPlugins.Add(pluginId);
            
            _logger.LogInformation("Enabled plugin: {Id}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling plugin {Id}", pluginId);
            return false;
        }
    }

    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                _logger.LogWarning("Plugin not found: {Id}", pluginId);
                return false;
            }

            if (!_enabledPlugins.Contains(pluginId))
            {
                _logger.LogInformation("Plugin already disabled: {Id}", pluginId);
                return true;
            }

            await plugin.ShutdownAsync();
            _enabledPlugins.Remove(pluginId);
            
            _logger.LogInformation("Disabled plugin: {Id}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling plugin {Id}", pluginId);
            return false;
        }
    }

    public IEnumerable<IPlugin> GetEnabledPlugins()
    {
        return _enabledPlugins.Select(id => _plugins[id]);
    }
}