using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Base interface for plugins
/// </summary>
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(ILogger logger);
    Task ShutdownAsync();
}