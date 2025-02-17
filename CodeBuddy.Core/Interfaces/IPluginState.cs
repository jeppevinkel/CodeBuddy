using System.Text.Json.Nodes;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Represents plugin state management functionality
/// </summary>
public interface IPluginState
{
    /// <summary>
    /// Gets the current state data
    /// </summary>
    JsonObject State { get; }
    
    /// <summary>
    /// The current state version number
    /// </summary>
    int StateVersion { get; }
    
    /// <summary>
    /// Updates the plugin state with new data
    /// </summary>
    /// <param name="state">The new state data</param>
    Task UpdateAsync(JsonObject state);
    
    /// <summary>
    /// Performs state migration from one version to another
    /// </summary>
    /// <param name="fromVersion">Source version number</param>
    /// <param name="toVersion">Target version number</param>
    Task MigrateAsync(int fromVersion, int toVersion);
    
    /// <summary>
    /// Clears the plugin state
    /// </summary>
    Task ClearAsync();
    
    /// <summary>
    /// Gets plugin health metrics and diagnostics
    /// </summary>
    /// <returns>Health metrics data</returns>
    Task<JsonObject> GetHealthMetricsAsync();
}