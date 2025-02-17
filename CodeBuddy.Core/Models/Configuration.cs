using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Represents application configuration
/// </summary>
public class Configuration
{
    public string TemplatesDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string PluginsDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> DefaultParameters { get; set; } = new();
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public bool EnablePlugins { get; set; } = true;
}