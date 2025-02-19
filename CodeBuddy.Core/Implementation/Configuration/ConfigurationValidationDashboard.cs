using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Provides real-time monitoring and visualization of configuration health across the application and its plugins.
    /// </summary>
    public class ConfigurationValidationDashboard
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationValidator _configValidator;
        private readonly IConfigurationMigrationManager _migrationManager;
        private readonly IPluginManager _pluginManager;
        private readonly ILoggingService _logger;

        public ConfigurationValidationDashboard(
            IConfigurationManager configManager,
            IConfigurationValidator configValidator,
            IConfigurationMigrationManager migrationManager,
            IPluginManager pluginManager,
            ILoggingService logger)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
            _migrationManager = migrationManager ?? throw new ArgumentNullException(nameof(migrationManager));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the overall configuration health status across all components
        /// </summary>
        public async Task<ConfigurationHealthStatus> GetConfigurationHealthOverviewAsync()
        {
            var healthStatus = new ConfigurationHealthStatus
            {
                Timestamp = DateTime.UtcNow,
                ValidationResults = await _configValidator.ValidateAllConfigurationsAsync(),
                SchemaVersionCompliance = await CheckSchemaVersionComplianceAsync(),
                MigrationStatus = await _migrationManager.GetMigrationStatusAsync(),
                PluginConfigurationStates = await GetPluginConfigurationStatesAsync()
            };

            return healthStatus;
        }

        /// <summary>
        /// Checks for and reports any configuration warnings or issues
        /// </summary>
        public async Task<List<ConfigurationWarning>> GetConfigurationWarningsAsync()
        {
            var warnings = new List<ConfigurationWarning>();
            
            // Check for deprecated configurations
            var deprecatedConfigs = await _configManager.GetDeprecatedConfigurationsAsync();
            warnings.AddRange(deprecatedConfigs.Select(c => new ConfigurationWarning
            {
                Type = WarningType.DeprecatedConfiguration,
                Message = $"Configuration '{c.Name}' is deprecated and will be removed in version {c.RemovalVersion}",
                Severity = WarningSeverity.Warning
            }));

            // Check for pending migrations
            var pendingMigrations = await _migrationManager.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                warnings.Add(new ConfigurationWarning
                {
                    Type = WarningType.RequiredMigration,
                    Message = $"{pendingMigrations.Count} configuration migrations pending",
                    Severity = WarningSeverity.Warning
                });
            }

            // Check for invalid configurations
            var validationResults = await _configValidator.ValidateAllConfigurationsAsync();
            warnings.AddRange(validationResults.Where(r => !r.IsValid).Select(r => new ConfigurationWarning
            {
                Type = WarningType.InvalidConfiguration,
                Message = $"Invalid configuration in {r.ConfigurationPath}: {r.ValidationMessage}",
                Severity = WarningSeverity.Error
            }));

            return warnings;
        }

        /// <summary>
        /// Gets the validation status for each component
        /// </summary>
        public async Task<Dictionary<string, ComponentValidationStatus>> GetValidationStatusByComponentAsync()
        {
            var componentStatuses = new Dictionary<string, ComponentValidationStatus>();

            // Core system validation status
            componentStatuses["Core"] = await GetCoreValidationStatusAsync();

            // Plugin validation statuses
            var plugins = await _pluginManager.GetAllPluginsAsync();
            foreach (var plugin in plugins)
            {
                componentStatuses[plugin.Name] = await GetPluginValidationStatusAsync(plugin.Name);
            }

            return componentStatuses;
        }

        /// <summary>
        /// Gets the configuration migration history
        /// </summary>
        public async Task<List<MigrationHistoryEntry>> GetMigrationHistoryAsync()
        {
            return await _migrationManager.GetMigrationHistoryAsync();
        }

        /// <summary>
        /// Gets environment-specific configuration status
        /// </summary>
        public async Task<EnvironmentConfigurationStatus> GetEnvironmentConfigurationStatusAsync(string environment)
        {
            return new EnvironmentConfigurationStatus
            {
                Environment = environment,
                ConfigurationSnapshot = await _configManager.GetEnvironmentConfigurationAsync(environment),
                ValidationStatus = await _configValidator.ValidateEnvironmentConfigurationAsync(environment),
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Handles critical configuration issues by logging and triggering alerts
        /// </summary>
        public async Task HandleCriticalConfigurationIssueAsync(ConfigurationWarning warning)
        {
            await _logger.LogErrorAsync($"Critical configuration issue detected: {warning.Message}");
            
            // Trigger alerts based on severity
            if (warning.Severity >= WarningSeverity.Error)
            {
                await TriggerConfigurationAlertAsync(warning);
            }
        }

        private async Task<List<SchemaVersionStatus>> CheckSchemaVersionComplianceAsync()
        {
            // Implementation for checking schema version compliance
            var statuses = new List<SchemaVersionStatus>();
            var configurations = await _configManager.GetAllConfigurationsAsync();
            
            foreach (var config in configurations)
            {
                statuses.Add(new SchemaVersionStatus
                {
                    ConfigurationName = config.Name,
                    CurrentVersion = config.Version,
                    ExpectedVersion = await _configManager.GetLatestSchemaVersionAsync(config.Name),
                    IsCompliant = await _configManager.IsSchemaVersionCompliantAsync(config.Name, config.Version)
                });
            }

            return statuses;
        }

        private async Task<Dictionary<string, PluginConfigurationState>> GetPluginConfigurationStatesAsync()
        {
            var states = new Dictionary<string, PluginConfigurationState>();
            var plugins = await _pluginManager.GetAllPluginsAsync();

            foreach (var plugin in plugins)
            {
                states[plugin.Name] = new PluginConfigurationState
                {
                    IsConfigured = await _pluginManager.IsPluginConfiguredAsync(plugin.Name),
                    ConfigurationStatus = await _configValidator.ValidatePluginConfigurationAsync(plugin.Name),
                    LastValidated = DateTime.UtcNow
                };
            }

            return states;
        }

        private async Task<ComponentValidationStatus> GetCoreValidationStatusAsync()
        {
            return new ComponentValidationStatus
            {
                ComponentName = "Core",
                ValidationResults = await _configValidator.ValidateCoreConfigurationsAsync(),
                LastValidated = DateTime.UtcNow
            };
        }

        private async Task<ComponentValidationStatus> GetPluginValidationStatusAsync(string pluginName)
        {
            return new ComponentValidationStatus
            {
                ComponentName = pluginName,
                ValidationResults = await _configValidator.ValidatePluginConfigurationAsync(pluginName),
                LastValidated = DateTime.UtcNow
            };
        }

        private async Task TriggerConfigurationAlertAsync(ConfigurationWarning warning)
        {
            // Alert implementation
            // TODO: Integrate with alert system
            await _logger.LogWarningAsync($"Configuration Alert: {warning.Message}");
        }
    }
}