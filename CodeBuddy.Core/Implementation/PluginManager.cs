using System.Reflection;
using System.Text.Json.Nodes;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements plugin management with dynamic loading, hot-reload, and health monitoring capabilities
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly ILogger _pluginLogger;
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly HashSet<string> _enabledPlugins = new();
    private readonly Dictionary<string, JsonObject> _pluginConfigs = new();
    private readonly Dictionary<string, JsonObject> _pluginStates = new();
    private readonly PluginWatcher _pluginWatcher;
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly SemaphoreSlim _reloadLock = new(1);
    private string? _monitoredDirectory;

    public PluginManager(
        ILogger<PluginManager> logger, 
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _pluginLogger = loggerFactory.CreateLogger("Plugins");
        _healthMonitor = new PluginHealthMonitor(loggerFactory.CreateLogger<PluginHealthMonitor>());
        _pluginWatcher = new PluginWatcher(
            loggerFactory.CreateLogger<PluginWatcher>(),
            HandlePluginFileChange);
    }

    public async Task<IEnumerable<IPlugin>> LoadPluginsAsync(string directory)
    {
        try
        {
            await _reloadLock.WaitAsync();
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
                        var pluginsFromFile = await LoadPluginsFromFileAsync(pluginFile);
                        discoveredPlugins.AddRange(pluginsFromFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error discovering plugin from {File}", pluginFile);
                    }
                }

                // Second pass: Validate dependencies and initialize in correct order
                var loadedPlugins = await InitializePluginsInOrderAsync(discoveredPlugins);
                return loadedPlugins;
            }
            finally
            {
                _reloadLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins from directory {Directory}", directory);
            throw;
        }
    }

    private async Task<IEnumerable<IPlugin>> LoadPluginsFromFileAsync(string pluginFile)
    {
        var plugins = new List<IPlugin>();
        var assembly = Assembly.LoadFrom(pluginFile);
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var pluginType in pluginTypes)
        {
            var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            _plugins[plugin.Id] = plugin;
            plugins.Add(plugin);
            _healthMonitor.RegisterPlugin(plugin);
            
            _logger.LogInformation("Discovered plugin: {Id} ({Name} v{Version})", 
                plugin.Id, plugin.Name, plugin.Version);
        }

        return plugins;
    }

    private async Task<IEnumerable<IPlugin>> InitializePluginsInOrderAsync(IEnumerable<IPlugin> discoveredPlugins)
    {
        var loadedPlugins = new List<IPlugin>();
        var remainingPlugins = new HashSet<IPlugin>(discoveredPlugins);
        var currentIteration = 0;
        var maxIterations = discoveredPlugins.Count();

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
                    await InitializePluginAsync(plugin);
                    loadedPlugins.Add(plugin);
                    remainingPlugins.Remove(plugin);
                    _healthMonitor.UpdatePluginState(plugin.Id, PluginState.Running);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing plugin {Id}", plugin.Id);
                    _healthMonitor.RecordError(plugin.Id, ex, false);
                    remainingPlugins.Remove(plugin);
                }
            }
        }

        return loadedPlugins;
    }

    private async Task InitializePluginAsync(IPlugin plugin)
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
        _logger.LogInformation("Initialized plugin: {Id}", plugin.Id);
    }

    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        await _reloadLock.WaitAsync();
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

            _healthMonitor.UpdatePluginState(pluginId, PluginState.Initializing);

            try
            {
                await InitializePluginAsync(plugin);
                _enabledPlugins.Add(pluginId);
                _healthMonitor.UpdatePluginState(pluginId, PluginState.Running);
                
                _logger.LogInformation("Enabled plugin: {Id}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _healthMonitor.RecordError(pluginId, ex, false);
                _logger.LogError(ex, "Error enabling plugin {Id}", pluginId);
                return false;
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        await _reloadLock.WaitAsync();
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

            _healthMonitor.UpdatePluginState(pluginId, PluginState.Disabled);

            try
            {
                // Save state before shutdown
                _pluginStates[pluginId] = plugin.State.State;

                await plugin.ShutdownAsync();
                _enabledPlugins.Remove(pluginId);
                
                _logger.LogInformation("Disabled plugin: {Id}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _healthMonitor.RecordError(pluginId, ex, true);
                _logger.LogError(ex, "Error disabling plugin {Id}", pluginId);
                return false;
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public IEnumerable<IPlugin> GetEnabledPlugins()
    {
        return _enabledPlugins.Select(id => _plugins[id]);
    }

    public async Task StartPluginMonitoringAsync(string directory)
    {
        await _reloadLock.WaitAsync();
        try
        {
            if (_monitoredDirectory != null)
            {
                _logger.LogWarning("Already monitoring directory: {Directory}", _monitoredDirectory);
                return;
            }

            _monitoredDirectory = directory;
            await _pluginWatcher.StartWatchingAsync(directory);
            _logger.LogInformation("Started plugin monitoring in directory: {Directory}", directory);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task StopPluginMonitoringAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            await _pluginWatcher.StopWatchingAsync();
            _monitoredDirectory = null;
            _logger.LogInformation("Stopped plugin monitoring");
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task<bool> ReloadPluginAsync(string pluginId)
    {
        await _reloadLock.WaitAsync();
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                _logger.LogWarning("Plugin not found: {Id}", pluginId);
                return false;
            }

            _healthMonitor.UpdatePluginState(pluginId, PluginState.Reloading);

            try
            {
                // Save state before unload
                _pluginStates[pluginId] = plugin.State.State;

                // Disable plugin and its dependents
                var wasEnabled = _enabledPlugins.Contains(pluginId);
                if (wasEnabled)
                {
                    await DisablePluginAsync(pluginId);
                }

                // Remove from tracking
                _plugins.Remove(pluginId);

                if (_monitoredDirectory == null)
                {
                    _logger.LogError("No monitored directory set for plugin reload");
                    return false;
                }

                // Reload plugin from file
                var pluginFile = Directory.GetFiles(_monitoredDirectory, "*.dll")
                    .FirstOrDefault(f => Assembly.LoadFrom(f).GetTypes()
                        .Any(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract &&
                                 ((IPlugin)Activator.CreateInstance(t)!).Id == pluginId));

                if (pluginFile == null)
                {
                    _logger.LogError("Could not find plugin file for {Id}", pluginId);
                    return false;
                }

                var loadedPlugins = await LoadPluginsFromFileAsync(pluginFile);
                var reloadedPlugin = loadedPlugins.FirstOrDefault(p => p.Id == pluginId);

                if (reloadedPlugin == null)
                {
                    _logger.LogError("Could not reload plugin {Id}", pluginId);
                    return false;
                }

                // Re-enable if it was enabled before
                if (wasEnabled)
                {
                    await EnablePluginAsync(pluginId);
                }

                _logger.LogInformation("Successfully reloaded plugin: {Id}", pluginId);
                _healthMonitor.UpdatePluginState(pluginId, wasEnabled ? PluginState.Running : PluginState.Disabled);
                return true;
            }
            catch (Exception ex)
            {
                _healthMonitor.RecordError(pluginId, ex, true);
                _logger.LogError(ex, "Error reloading plugin {Id}", pluginId);
                return false;
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public Task<PluginHealthStatus> GetPluginHealthAsync(string pluginId)
    {
        var status = _healthMonitor.GetPluginHealth(pluginId);
        return Task.FromResult(status ?? new PluginHealthStatus
        {
            PluginId = pluginId,
            State = PluginState.Unknown
        });
    }

    public Task<IEnumerable<PluginHealthStatus>> GetAllPluginsHealthAsync()
    {
        return Task.FromResult(_healthMonitor.GetAllPluginsHealth());
    }

    private async void HandlePluginFileChange(string pluginPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(pluginPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var pluginType in pluginTypes)
            {
                var tempPlugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                if (_plugins.ContainsKey(tempPlugin.Id))
                {
                    _logger.LogInformation("Detected changes in plugin: {Id}", tempPlugin.Id);
                    await ReloadPluginAsync(tempPlugin.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling plugin file change: {Path}", pluginPath);
        }
    }
}