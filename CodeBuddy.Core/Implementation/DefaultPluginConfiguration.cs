using System.Text.Json.Nodes;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Default implementation of plugin configuration management
/// </summary>
public class DefaultPluginConfiguration : IPluginConfiguration
{
    private readonly ILogger _logger;
    private readonly string _pluginId;
    private JsonObject _current;

    public DefaultPluginConfiguration(
        string pluginId,
        JsonObject schema,
        JsonObject defaults,
        ILogger logger)
    {
        _pluginId = pluginId;
        _logger = logger;
        Schema = schema;
        Defaults = defaults;
        _current = new JsonObject(defaults);
    }

    public JsonObject Schema { get; }
    public JsonObject Defaults { get; }
    public JsonObject Current => _current;

    public async Task<bool> UpdateAsync(JsonObject values)
    {
        try
        {
            if (!Validate(values))
            {
                _logger.LogError("Configuration validation failed for plugin {Id}", _pluginId);
                return false;
            }

            _current = values;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for plugin {Id}", _pluginId);
            return false;
        }
    }

    public bool Validate(JsonObject values)
    {
        try
        {
            // Basic validation - ensure all required properties exist
            foreach (var required in Schema["required"]?.AsArray() ?? new JsonArray())
            {
                var propName = required?.GetValue<string>();
                if (propName != null && !values.ContainsKey(propName))
                {
                    _logger.LogError("Missing required configuration property {Property} for plugin {Id}",
                        propName, _pluginId);
                    return false;
                }
            }

            // Type validation could be added here
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration for plugin {Id}", _pluginId);
            return false;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        _current = new JsonObject(Defaults);
    }
}