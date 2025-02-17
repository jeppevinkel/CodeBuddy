namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages plugin lifecycle and operations
/// </summary>
public interface IPluginManager
{
    Task<IEnumerable<IPlugin>> LoadPluginsAsync(string directory);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    IEnumerable<IPlugin> GetEnabledPlugins();
}