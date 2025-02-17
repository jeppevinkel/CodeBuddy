using System.Text.Json.Nodes;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Represents a plugin dependency
/// </summary>
public interface IPluginDependency
{
    /// <summary>
    /// The ID of the required plugin
    /// </summary>
    string PluginId { get; }
    
    /// <summary>
    /// The minimum required version of the plugin
    /// </summary>
    string MinVersion { get; }
    
    /// <summary>
    /// The maximum compatible version of the plugin (optional)
    /// </summary>
    string? MaxVersion { get; }
    
    /// <summary>
    /// Whether this is an optional dependency
    /// </summary>
    bool IsOptional { get; }
}