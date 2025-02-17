using System.Reflection;
using System.Text.Json.Nodes;
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
    private readonly Dictionary<string, JsonObject> _pluginConfigs = new();
    private readonly Dictionary<string, JsonObject> _pluginStates = new();

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
            var discoveredPlugins = new List<IPlugin>();

            // First pass: Discover all plugins
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
                        _plugins[plugin.Id] = plugin;
                        discoveredPlugins.Add(plugin);
                        
                        _logger.LogInformation("Discovered plugin: {Id} ({Name} v{Version})", 
                            plugin.Id, plugin.Name, plugin.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error discovering plugin from {File}", pluginFile);
                }
            }

            // Second pass: Validate dependencies and initialize in correct order
            var loadedPlugins = new List<IPlugin>();
            var remainingPlugins = new HashSet<IPlugin>(discoveredPlugins);
            var currentIteration = 0;
            var maxIterations = discoveredPlugins.Count;

            while (remainingPlugins.Count > 0 && currentIteration < maxIterations)
            {
                currentIteration++;
                var pluginsToLoad = remainingPlugins
                    .Where(p => p.ValidateDependencies(loadedPlugins))
                    .ToList();

                if (!pluginsToLoad.Any())
                {
                    _logger.LogError("Circular or unsatisfiable dependencies detected");
                    break;
                }

                foreach (var plugin in pluginsToLoad)
                {
                    try
                    {
                        // Load configuration if exists
                        if (!_pluginConfigs.TryGetValue(plugin.Id, out var config))
                        {
                            config = plugin.Configuration.Defaults;
                            _pluginConfigs[plugin.Id] = config;
                        }

                        // Load state if exists
                        if (!_pluginStates.TryGetValue(plugin.Id, out var state))
                        {
                            state = new JsonObject();
                            _pluginStates[plugin.Id] = state;
                        }

                        var context = new JsonObject
                        {
                            ["config"] = config,
                            ["state"] = state
                        };

                        await plugin.InitializeAsync(_pluginLogger, context);
                        loadedPlugins.Add(plugin);
                        remainingPlugins.Remove(plugin);
                        
                        _logger.LogInformation("Initialized plugin: {Id}", plugin.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing plugin {Id}", plugin.Id);
                        remainingPlugins.Remove(plugin);
                    }
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

            // Check dependencies
            if (!plugin.ValidateDependencies(GetEnabledPlugins()))
            {
                _logger.LogError("Cannot enable plugin {Id}: dependencies not satisfied", pluginId);
                return false;
            }

            // Load configuration and state
            var context = new JsonObject
            {
                ["config"] = _pluginConfigs.GetValueOrDefault(pluginId, plugin.Configuration.Defaults),
                ["state"] = _pluginStates.GetValueOrDefault(pluginId, new JsonObject())
            };

            await plugin.InitializeAsync(_pluginLogger, context);
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

            // Check if other enabled plugins depend on this one
            var dependentPlugins = GetEnabledPlugins()
                .Where(p => p.Id != pluginId && p.Dependencies.Any(d => d.PluginId == pluginId && !d.IsOptional))
                .ToList();

            if (dependentPlugins.Any())
            {
                var dependentIds = string.Join(", ", dependentPlugins.Select(p => p.Id));
                _logger.LogError("Cannot disable plugin {Id}: required by {DependentIds}", pluginId, dependentIds);
                return false;
            }

            // Save state before shutdown
            _pluginStates[pluginId] = plugin.State.State;

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