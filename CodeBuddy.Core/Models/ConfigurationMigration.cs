using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Handles configuration migrations between versions
/// </summary>
public class ConfigurationMigration
{
    private readonly Dictionary<Version, Action<JObject>> _migrations;
    private static readonly Version CurrentVersion = new(1, 0, 0);

    public ConfigurationMigration()
    {
        _migrations = new Dictionary<Version, Action<JObject>>
        {
            { new Version(1, 0, 0), MigrateTo1_0_0 }
        };
    }

    /// <summary>
    /// Migrates configuration from one version to another
    /// </summary>
    public string MigrateConfiguration(string jsonConfig, Version fromVersion)
    {
        var config = JObject.Parse(jsonConfig);
        var currentVersion = fromVersion;

        while (currentVersion < CurrentVersion)
        {
            var nextVersion = _migrations.Keys
                .Where(v => v > currentVersion)
                .OrderBy(v => v)
                .FirstOrDefault();

            if (nextVersion == null)
                break;

            _migrations[nextVersion](config);
            currentVersion = nextVersion;
        }

        // Add version information
        config["Version"] = CurrentVersion.ToString();

        return config.ToString();
    }

    /// <summary>
    /// Migration to version 1.0.0
    /// - Adds encryption for sensitive data
    /// - Adds environment-specific configuration support
    /// - Adds validation rules
    /// </summary>
    private void MigrateTo1_0_0(JObject config)
    {
        // Move any existing sensitive data to the new Secrets dictionary
        var secrets = new JObject();
        var propertiesToRemove = new List<string>();

        foreach (var property in config.Properties())
        {
            var propertyInfo = typeof(Configuration).GetProperty(property.Name);
            if (propertyInfo?.GetCustomAttribute<EncryptedAttribute>() != null)
            {
                secrets[property.Name] = property.Value;
                propertiesToRemove.Add(property.Name);
            }
        }

        foreach (var prop in propertiesToRemove)
        {
            config.Remove(prop);
        }

        if (secrets.Count > 0)
        {
            config["Secrets"] = secrets;
        }

        // Add default environment configuration if none exists
        if (!config.ContainsKey("EnvironmentConfigs"))
        {
            config["EnvironmentConfigs"] = new JArray(
                new JObject
                {
                    ["Environment"] = "Development",
                    ["Overrides"] = new JObject()
                }
            );
        }

        // Ensure default values for required properties
        if (!config.ContainsKey("MinimumLogLevel"))
        {
            config["MinimumLogLevel"] = "Information";
        }

        if (!config.ContainsKey("EnablePlugins"))
        {
            config["EnablePlugins"] = true;
        }
    }
}

/// <summary>
/// Exception thrown when configuration migration fails
/// </summary>
public class ConfigurationMigrationException : Exception
{
    public Version FromVersion { get; }
    public Version ToVersion { get; }

    public ConfigurationMigrationException(Version from, Version to, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FromVersion = from;
        ToVersion = to;
    }
}