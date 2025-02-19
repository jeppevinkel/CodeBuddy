using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration.Migrations
{
    /// <summary>
    /// Base class for configuration migration scripts
    /// </summary>
    public abstract class ConfigurationMigrationScript<T> : IConfigurationMigration<T> where T : BaseConfiguration
    {
        public Version FromVersion { get; }
        public Version ToVersion { get; }
        
        protected ConfigurationMigrationScript(string fromVersion, string toVersion)
        {
            FromVersion = Version.Parse(fromVersion);
            ToVersion = Version.Parse(toVersion);
        }

        public abstract Task<T> MigrateConfigurationAsync(T configuration);
        
        protected void EnsureVersion(T configuration)
        {
            if (configuration.SchemaVersion != FromVersion)
            {
                throw new InvalidOperationException(
                    $"Configuration version mismatch. Expected {FromVersion}, got {configuration.SchemaVersion}");
            }
        }
    }

    /// <summary>
    /// Example migration script for plugin configuration from 1.0 to 1.1
    /// </summary>
    public class PluginConfigV1_0ToV1_1 : ConfigurationMigrationScript<PluginConfiguration>
    {
        public PluginConfigV1_0ToV1_1() : base("1.0", "1.1") { }

        public override async Task<PluginConfiguration> MigrateConfigurationAsync(PluginConfiguration configuration)
        {
            EnsureVersion(configuration);

            // Migrate configuration
            configuration.SchemaVersion = ToVersion;
            
            // Add new fields with defaults
            if (configuration.FeatureFlags == null)
            {
                configuration.FeatureFlags = new Dictionary<string, bool>();
            }
            
            if (configuration.Dependencies == null)
            {
                configuration.Dependencies = Array.Empty<string>();
            }

            // Set default values for new properties
            if (configuration.InitializationTimeoutSeconds == 0)
            {
                configuration.InitializationTimeoutSeconds = 30;
            }

            if (configuration.ExecutionTimeoutSeconds == 0)
            {
                configuration.ExecutionTimeoutSeconds = 300;
            }

            return configuration;
        }
    }

    /// <summary>
    /// Example migration script for secure configuration from 1.0 to 1.1
    /// </summary>
    public class SecureConfigV1_0ToV1_1 : ConfigurationMigrationScript<SecureConfiguration>
    {
        private readonly IEncryptionProvider _encryptionProvider;

        public SecureConfigV1_0ToV1_1(IEncryptionProvider encryptionProvider) 
            : base("1.0", "1.1")
        {
            _encryptionProvider = encryptionProvider;
        }

        public override async Task<SecureConfiguration> MigrateConfigurationAsync(SecureConfiguration configuration)
        {
            EnsureVersion(configuration);

            // Re-encrypt sensitive data with new encryption method
            if (configuration.SecureData != null)
            {
                var decrypted = await _encryptionProvider.DecryptLegacyAsync(configuration.SecureData);
                configuration.SecureData = await _encryptionProvider.EncryptAsync(decrypted);
            }

            configuration.SchemaVersion = ToVersion;
            return configuration;
        }
    }

    /// <summary>
    /// Example migration script for validation configuration from 1.0 to 1.1
    /// </summary>
    public class ValidationConfigV1_0ToV1_1 : ConfigurationMigrationScript<ValidationConfiguration>
    {
        public ValidationConfigV1_0ToV1_1() : base("1.0", "1.1") { }

        public override async Task<ValidationConfiguration> MigrateConfigurationAsync(ValidationConfiguration configuration)
        {
            EnsureVersion(configuration);

            // Update validation rules format
            if (configuration.ValidationRules != null)
            {
                var updatedRules = new Dictionary<string, ValidationRule>();
                foreach (var rule in configuration.ValidationRules)
                {
                    // Convert old rule format to new format
                    updatedRules[rule.Key] = new ValidationRule
                    {
                        IsRequired = rule.Value.Required,
                        MinValue = rule.Value.Minimum,
                        MaxValue = rule.Value.Maximum,
                        Pattern = rule.Value.RegexPattern,
                        CustomValidation = rule.Value.ValidationType == "Custom"
                    };
                }
                configuration.ValidationRules = updatedRules;
            }

            configuration.SchemaVersion = ToVersion;
            return configuration;
        }
    }
}