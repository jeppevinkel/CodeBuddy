using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Handles configuration migrations between schema versions
    /// </summary>
    public class ConfigurationMigrationManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<(Type Type, Version From, Version To), IConfigurationMigration> _migrations = new();

        public ConfigurationMigrationManager(ILogger logger)
        {
            _logger = logger;
        }

        public bool NeedsMigration(string section, object configuration)
        {
            var configType = configuration.GetType();
            var currentVersion = GetCurrentSchemaVersion(configType);
            var configuredVersion = GetConfiguredVersion(section, configuration);

            return currentVersion > configuredVersion;
        }

        public async Task<MigrationResult> MigrateConfiguration(string section, object configuration)
        {
            try
            {
                var configType = configuration.GetType();
                var currentVersion = GetCurrentSchemaVersion(configType);
                var configuredVersion = GetConfiguredVersion(section, configuration);

                if (currentVersion <= configuredVersion)
                {
                    return new MigrationResult { Success = true, Configuration = configuration };
                }

                var currentConfig = configuration;
                var currentConfigVersion = configuredVersion;

                while (currentConfigVersion < currentVersion)
                {
                    var nextVersion = GetNextVersion(configType, currentConfigVersion);
                    if (!nextVersion.HasValue)
                    {
                        break;
                    }

                    var migration = GetMigration(configType, currentConfigVersion, nextVersion.Value);
                    if (migration == null)
                    {
                        _logger.LogWarning("No migration found from version {From} to {To} for type {Type}",
                            currentConfigVersion, nextVersion, configType.Name);
                        break;
                    }

                    try
                    {
                        currentConfig = await migration.MigrateAsync(currentConfig);
                        currentConfigVersion = nextVersion.Value;
                        
                        _logger.LogInformation("Successfully migrated configuration {Section} from version {From} to {To}",
                            section, currentConfigVersion, nextVersion);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error migrating configuration {Section} from version {From} to {To}",
                            section, currentConfigVersion, nextVersion);
                        return new MigrationResult 
                        { 
                            Success = false, 
                            Error = $"Migration failed: {ex.Message}",
                            Configuration = currentConfig 
                        };
                    }
                }

                return new MigrationResult { Success = true, Configuration = currentConfig };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration migration for section {Section}", section);
                return new MigrationResult { Success = false, Error = ex.Message };
            }
        }

        public void RegisterMigration<T>(Version fromVersion, Version toVersion, IConfigurationMigration<T> migration)
            where T : class
        {
            _migrations[(typeof(T), fromVersion, toVersion)] = migration;
        }

        private Version GetCurrentSchemaVersion(Type configType)
        {
            var schemaAttr = configType.GetCustomAttribute<SchemaVersionAttribute>();
            return schemaAttr?.Version ?? new Version(1, 0);
        }

        private Version GetConfiguredVersion(string section, object configuration)
        {
            var versionAttr = configuration.GetType()
                .GetCustomAttribute<SchemaVersionAttribute>();
            
            if (versionAttr != null)
            {
                return versionAttr.Version;
            }

            // Look for version in configuration data
            var versionProp = configuration.GetType()
                .GetProperties()
                .FirstOrDefault(p => p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase));
            
            if (versionProp != null)
            {
                var value = versionProp.GetValue(configuration)?.ToString();
                if (Version.TryParse(value, out var version))
                {
                    return version;
                }
            }

            return new Version(1, 0);
        }

        private Version? GetNextVersion(Type configType, Version currentVersion)
        {
            return _migrations.Keys
                .Where(k => k.Type == configType && k.From == currentVersion)
                .Select(k => k.To)
                .OrderBy(v => v)
                .FirstOrDefault();
        }

        private IConfigurationMigration? GetMigration(Type configType, Version fromVersion, Version toVersion)
        {
            return _migrations.TryGetValue((configType, fromVersion, toVersion), out var migration) 
                ? migration 
                : null;
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public object? Configuration { get; set; }
    }

    public interface IConfigurationMigration
    {
        Task<object> MigrateAsync(object configuration);
    }

    public interface IConfigurationMigration<T> : IConfigurationMigration where T : class
    {
        Task<T> MigrateConfigurationAsync(T configuration);
        
        async Task<object> IConfigurationMigration.MigrateAsync(object configuration)
        {
            if (configuration is T typedConfig)
            {
                return await MigrateConfigurationAsync(typedConfig);
            }
            throw new ArgumentException($"Configuration is not of type {typeof(T).Name}");
        }
    }
}