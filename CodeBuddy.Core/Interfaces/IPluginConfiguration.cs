using System.Text.Json.Nodes;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Represents plugin configuration management functionality
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Gets the configuration schema for validation
    /// </summary>
    JsonObject Schema { get; }
    
    /// <summary>
    /// Gets the default configuration values
    /// </summary>
    JsonObject Defaults { get; }
    
    /// <summary>
    /// Gets the current configuration values
    /// </summary>
    JsonObject Current { get; }
    
    /// <summary>
    /// Updates the configuration with new values
    /// </summary>
    /// <param name="values">The new configuration values</param>
    /// <returns>True if validation passes and update succeeds</returns>
    Task<bool> UpdateAsync(JsonObject values);
    
    /// <summary>
    /// Validates configuration values against the schema
    /// </summary>
    /// <param name="values">The configuration values to validate</param>
    /// <returns>True if validation passes</returns>
    bool Validate(JsonObject values);
    
    /// <summary>
    /// Resets configuration to default values
    /// </summary>
    Task ResetToDefaultsAsync();
}