using System.Text.Json.Nodes;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Default implementation of plugin state management
/// </summary>
public class DefaultPluginState : IPluginState
{
    private readonly ILogger _logger;
    private readonly string _pluginId;
    private JsonObject _state;
    private readonly List<string> _errors = new();

    public DefaultPluginState(string pluginId, ILogger logger)
    {
        _pluginId = pluginId;
        _logger = logger;
        _state = new JsonObject();
        StateVersion = 1;
    }

    public JsonObject State => _state;
    public int StateVersion { get; private set; }

    public async Task UpdateAsync(JsonObject state)
    {
        try
        {
            _state = state;
            _logger.LogInformation("Updated state for plugin {Id}", _pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating state for plugin {Id}", _pluginId);
            _errors.Add($"State update failed: {ex.Message}");
            throw;
        }
    }

    public async Task MigrateAsync(int fromVersion, int toVersion)
    {
        try
        {
            if (fromVersion >= toVersion)
            {
                _logger.LogWarning("Invalid migration request: from {FromVersion} to {ToVersion}", 
                    fromVersion, toVersion);
                return;
            }

            // Implement state migration logic here
            // This is a placeholder that just updates the version
            StateVersion = toVersion;
            
            _logger.LogInformation("Migrated state for plugin {Id} from v{FromVersion} to v{ToVersion}",
                _pluginId, fromVersion, toVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating state for plugin {Id}", _pluginId);
            _errors.Add($"State migration failed: {ex.Message}");
            throw;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            _state = new JsonObject();
            _errors.Clear();
            _logger.LogInformation("Cleared state for plugin {Id}", _pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing state for plugin {Id}", _pluginId);
            throw;
        }
    }

    public async Task<JsonObject> GetHealthMetricsAsync()
    {
        var metrics = new JsonObject
        {
            ["stateVersion"] = StateVersion,
            ["stateSize"] = _state.ToJsonString().Length,
            ["errors"] = new JsonArray(_errors.Select(e => JsonValue.Create(e)).ToArray()),
            ["lastUpdateTime"] = JsonValue.Create(DateTime.UtcNow)
        };

        return metrics;
    }
}