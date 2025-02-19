using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages application configuration with support for multiple sources, validation, and secure storage
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
        public ConfigurationManager(
            IConfigurationValidator validator,
            IConfigurationMigrationManager migrationManager,
            ILoggingService logger,
            SystemHealthDashboard systemHealthDashboard,
            IResourceMonitor resourceMonitor)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _migrationManager = migrationManager ?? throw new ArgumentNullException(nameof(migrationManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _systemHealthDashboard = systemHealthDashboard ?? throw new ArgumentNullException(nameof(systemHealthDashboard));
            _validationDashboard = new ConfigurationValidationDashboard(this, validator, migrationManager, null, logger, systemHealthDashboard, resourceMonitor);
            public async Task<List<ConfigurationWarning>> ValidateAndMonitorConfigurationAsync()
        {
            var warnings = await _validationDashboard.GetConfigurationWarningsAsync();
            
            foreach (var warning in warnings.Where(w => w.Severity >= WarningSeverity.Error))
            {
                await _validationDashboard.HandleCriticalConfigurationIssueAsync(warning);
            }

            return warnings;
        }

        public async Task<ConfigurationHealthStatus> GetConfigurationHealthStatusAsync()
        {
            return await _validationDashboard.GetConfigurationHealthOverviewAsync();
        }

        public async Task<List<SchemaVersionStatus>> GetSchemaVersionComplianceAsync()
        {
            var configurations = await GetAllConfigurationsAsync();
            var statuses = new List<SchemaVersionStatus>();
            
            foreach (var config in configurations)
            {
                var latestVersion = await GetLatestSchemaVersionAsync(config.Name);
                statuses.Add(new SchemaVersionStatus
                {
                    ConfigurationName = config.Name,
                    CurrentVersion = config.Version,
                    ExpectedVersion = latestVersion,
                    IsCompliant = await IsSchemaVersionCompliantAsync(config.Name, config.Version)
                });
            }

            return statuses;
        }

        public async Task<bool> IsSchemaVersionCompliantAsync(string configName, string version)
        {
            // Implementation for schema version compliance check
            return true; // TODO: Implement actual version comparison logic
        }

        public async Task<string> GetLatestSchemaVersionAsync(string configName)
        {
            // Implementation to get the latest schema version
            return "1.0.0"; // TODO: Implement actual version retrieval logic
        }

        public async Task<List<object>> GetAllConfigurationsAsync()
        {
            // Implementation to get all configurations
            return new List<object>(); // TODO: Implement actual configuration retrieval
        }

        public async Task<object> GetEnvironmentConfigurationAsync(string environment)
        {
            // Implementation to get environment-specific configuration
            return null; // TODO: Implement actual environment configuration retrieval
        }

        public async Task<List<(string Name, string Version, DateTime RemovalDate)>> GetDeprecatedConfigurationsAsync()
        {
            // Implementation to get deprecated configurations
            return new List<(string Name, string Version, DateTime RemovalDate)>();
        }
    }

        private readonly IConfigurationValidator _validator;
        private readonly IConfigurationMigrationManager _migrationManager;
        private readonly ConfigurationValidationDashboard _validationDashboard;
        private readonly SystemHealthDashboard _systemHealthDashboard;
        private readonly ILoggingService _logger;
    {
        private readonly IConfigurationMigrationManager _migrationManager;
        private readonly IConfigurationValidator _validator;
        private readonly SecureConfigurationStorage _secureStorage;
        private readonly ConfigurationDocumentationGenerator _documentationGenerator;
        private readonly string _configBasePath;
        private readonly Dictionary<string, object> _configCache;
        private readonly Dictionary<string, List<Delegate>> _changeCallbacks;

        public ConfigurationManager(
            IConfigurationMigrationManager migrationManager,
            IConfigurationValidator validator,
            SecureConfigurationStorage secureStorage,
            ConfigurationDocumentationGenerator documentationGenerator,
            string configBasePath = "config")
        {
            _migrationManager = migrationManager ?? throw new ArgumentNullException(nameof(migrationManager));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _documentationGenerator = documentationGenerator ?? throw new ArgumentNullException(nameof(documentationGenerator));
            _configBasePath = configBasePath;
            _configCache = new Dictionary<string, object>();
            _changeCallbacks = new Dictionary<string, List<Delegate>>();
        }

        public async Task<T> GetConfiguration<T>(string section) where T : class, new()
        {
            return await GetConfiguration<T>(section, Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "development");
        }

        public async Task<T> GetConfiguration<T>(string section, string environment) where T : class, new()
        {
            var cacheKey = $"{section}_{environment}";
            
            if (_configCache.TryGetValue(cacheKey, out var cached))
            {
                return (T)cached;
            }

            var config = await LoadConfiguration<T>(section, environment);
            
            // Check if migration is needed
            var currentVersion = GetConfigurationVersion(section);
            config = await _migrationManager.MigrateIfNeeded<T>(config, section, currentVersion);
            
            // Validate configuration
            var validationResults = ValidateConfiguration(config);
            if (validationResults.Any())
            {
                throw new ValidationException($"Configuration validation failed for section {section}: " +
                    string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
            }

            _configCache[cacheKey] = config;
            return config;
        }

        public async Task SaveConfiguration<T>(string section, T configuration) where T : class
        {
            var validationResults = ValidateConfiguration(configuration);
            if (validationResults.Any())
            {
                throw new ValidationException($"Configuration validation failed for section {section}: " +
                    string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
            }

            var configPath = GetConfigPath(section);
            var jsonString = JsonSerializer.Serialize(configuration, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(configPath, jsonString);
            
            // Update cache and notify subscribers
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "development";
            var cacheKey = $"{section}_{environment}";
            _configCache[cacheKey] = configuration;
            
            NotifyConfigurationChanged(section, configuration);
        }

        public IEnumerable<ValidationResult> ValidateConfiguration<T>(T configuration) where T : class
        {
            return _validator.Validate(configuration);
        }

        public string GetConfigurationVersion(string section)
        {
            var configPath = GetConfigPath(section);
            if (!File.Exists(configPath))
            {
                return "1.0";
            }

            var configText = File.ReadAllText(configPath);
            var config = JsonDocument.Parse(configText);
            
            if (config.RootElement.TryGetProperty("version", out var versionElement))
            {
                return versionElement.GetString() ?? "1.0";
            }
            
            return "1.0";
        }

        public void RegisterChangeCallback<T>(string section, Action<T> callback) where T : class
        {
            if (!_changeCallbacks.ContainsKey(section))
            {
                _changeCallbacks[section] = new List<Delegate>();
            }
            
            _changeCallbacks[section].Add(callback);
        }

        public async Task<string> GetSecureValue(string section, string key)
        {
            return await _secureStorage.GetValue(section, key);
        }

        public async Task SetSecureValue(string section, string key, string value)
        {
            await _secureStorage.SetValue(section, key, value);
        }

        public async Task BackupConfiguration(string backupPath)
        {
            var configDir = new DirectoryInfo(_configBasePath);
            if (!configDir.Exists)
            {
                throw new DirectoryNotFoundException($"Configuration directory not found: {_configBasePath}");
            }

            var backupDir = new DirectoryInfo(backupPath);
            if (!backupDir.Exists)
            {
                backupDir.Create();
            }

            foreach (var file in configDir.GetFiles("*.json"))
            {
                var destPath = Path.Combine(backupPath, file.Name);
                file.CopyTo(destPath, true);
            }

            await _secureStorage.BackupSecureValues(backupPath);
        }

        public async Task RestoreConfiguration(string backupPath)
        {
            var backupDir = new DirectoryInfo(backupPath);
            if (!backupDir.Exists)
            {
                throw new DirectoryNotFoundException($"Backup directory not found: {backupPath}");
            }

            var configDir = new DirectoryInfo(_configBasePath);
            if (!configDir.Exists)
            {
                configDir.Create();
            }

            foreach (var file in backupDir.GetFiles("*.json"))
            {
                var destPath = Path.Combine(_configBasePath, file.Name);
                file.CopyTo(destPath, true);
            }

            await _secureStorage.RestoreSecureValues(backupPath);
            
            // Clear cache after restore
            _configCache.Clear();
        }

        public async Task<IDictionary<string, string>> GetConfigurationMetadata(string section)
        {
            var metadata = new Dictionary<string, string>();
            
            metadata["version"] = GetConfigurationVersion(section);
            metadata["lastModified"] = File.GetLastWriteTimeUtc(GetConfigPath(section)).ToString("O");
            metadata["size"] = new FileInfo(GetConfigPath(section)).Length.ToString();
            
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "development";
            metadata["environment"] = environment;
            
            return metadata;
        }

        public async Task<string> GenerateConfigurationDocumentation()
        {
            return await _documentationGenerator.GenerateDocumentation(_configBasePath);
        }

        private async Task<T> LoadConfiguration<T>(string section, string environment) where T : class, new()
        {
            var configPath = GetConfigPath(section);
            if (!File.Exists(configPath))
            {
                return new T();
            }

            var configText = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<T>(configText);

            // Apply environment variables override
            ApplyEnvironmentOverrides(config);

            return config ?? new T();
        }

        private void ApplyEnvironmentOverrides<T>(T config) where T : class
        {
            var prefix = $"CONFIG_{typeof(T).Name.ToUpperInvariant()}_";
            
            foreach (var prop in typeof(T).GetProperties())
            {
                var envVar = Environment.GetEnvironmentVariable($"{prefix}{prop.Name.ToUpperInvariant()}");
                if (!string.IsNullOrEmpty(envVar))
                {
                    var convertedValue = Convert.ChangeType(envVar, prop.PropertyType);
                    prop.SetValue(config, convertedValue);
                }
            }
        }

        private string GetConfigPath(string section)
        {
            return Path.Combine(_configBasePath, $"{section}.json");
        }

        private void NotifyConfigurationChanged<T>(string section, T configuration) where T : class
        {
            if (!_changeCallbacks.ContainsKey(section))
            {
                return;
            }

            foreach (var callback in _changeCallbacks[section].OfType<Action<T>>())
            {
                callback(configuration);
            }
        }
    }
}