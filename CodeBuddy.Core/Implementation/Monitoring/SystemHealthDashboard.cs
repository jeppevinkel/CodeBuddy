using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Implementation.Configuration;

namespace CodeBuddy.Core.Implementation.Monitoring
{
    /// <summary>
    /// Dashboard for monitoring system health including configuration status
    /// </summary>
    public class SystemHealthDashboard
    {
        private readonly ConfigurationValidationManager _validationManager;
        private readonly ConfigurationMigrationManager _migrationManager;
        private readonly Dictionary<Type, object> _activeConfigurations;
        private readonly List<ConfigurationHealthCheck> _healthChecks;
        private readonly ConfigurationValidationDashboard _configDashboard;

        public SystemHealthDashboard(
            ConfigurationValidationManager validationManager,
            ConfigurationMigrationManager migrationManager,
            ConfigurationValidationDashboard configDashboard)
        {
            _validationManager = validationManager;
            _migrationManager = migrationManager;
            _configDashboard = configDashboard;
            _activeConfigurations = new Dictionary<Type, object>();
            _healthChecks = new List<ConfigurationHealthCheck>();
        }

        /// <summary>
        /// Registers a configuration for health monitoring
        /// </summary>
        public void RegisterConfiguration<T>(T configuration) where T : BaseConfiguration
        {
            var configType = typeof(T);
            _activeConfigurations[configType] = configuration;
            
            // Schedule periodic health check
            _healthChecks.Add(new ConfigurationHealthCheck
            {
                ConfigurationType = configType,
                LastCheck = DateTime.UtcNow,
                Status = ConfigurationHealthStatus.Unknown,
                Issues = new List<string>()
            });
        }

        /// <summary>
        /// Performs health checks on all registered configurations
        /// </summary>
        public async Task<SystemHealthReport> CheckHealthAsync()
        {
            var report = new SystemHealthReport
            {
                Timestamp = DateTime.UtcNow,
                ConfigurationHealth = new List<ConfigurationHealthCheck>()
            };

            foreach (var check in _healthChecks)
            {
                try
                {
                    var config = _activeConfigurations[check.ConfigurationType];
                    var configType = check.ConfigurationType;
                    
                    // Update check timestamp
                    check.LastCheck = DateTime.UtcNow;
                    check.Issues.Clear();

                    // Validate configuration
                    var validationResult = await _validationManager.ValidateAsync(config);
                    if (!validationResult.IsValid)
                    {
                        check.Status = ConfigurationHealthStatus.Invalid;
                        check.Issues.AddRange(validationResult.Errors);
                    }

                    // Check for pending migrations
                    if (_migrationManager.RequiresMigration(config))
                    {
                        check.Status = ConfigurationHealthStatus.RequiresMigration;
                        check.Issues.Add($"Configuration requires migration to latest schema version");
                    }

                    // Check migration history for recent failures
                    var recentMigrations = _migrationManager
                        .GetMigrationHistory(configType)
                        .Where(m => m.MigrationDate >= DateTime.UtcNow.AddDays(-1));

                    if (recentMigrations.Any(m => !m.Success))
                    {
                        check.Status = ConfigurationHealthStatus.MigrationFailed;
                        check.Issues.Add("Recent configuration migration failed");
                    }

                    // Check environment-specific settings
                    ValidateEnvironmentSettings(config, check);

                    // Set status to healthy if no issues found
                    if (!check.Issues.Any())
                    {
                        check.Status = ConfigurationHealthStatus.Healthy;
                    }

                    report.ConfigurationHealth.Add(check);
                }
                catch (Exception ex)
                {
                    check.Status = ConfigurationHealthStatus.Error;
                    check.Issues.Add($"Health check failed: {ex.Message}");
                    report.ConfigurationHealth.Add(check);
                }
            }

            return report;
        }

        /// <summary>
        /// Gets configuration health metrics from the validation dashboard
        /// </summary>
        public async Task<ConfigurationHealthStatus> GetConfigurationHealthMetricsAsync()
        {
            return await _configDashboard.GetConfigurationHealthOverviewAsync();
        }

        /// <summary>
        /// Gets validation status by component from the validation dashboard
        /// </summary>
        public async Task<Dictionary<string, ComponentValidationStatus>> GetValidationStatusByComponentAsync()
        {
            return await _configDashboard.GetValidationStatusByComponentAsync();
        }

        /// <summary>
        /// Gets migration history from the validation dashboard
        /// </summary>
        public async Task<List<MigrationHistoryEntry>> GetMigrationHistoryAsync()
        {
            return await _configDashboard.GetMigrationHistoryAsync();
        }

        /// <summary>
        /// Gets environment-specific configuration status from the validation dashboard
        /// </summary>
        public async Task<EnvironmentConfigurationStatus> GetEnvironmentConfigurationStatusAsync(string environment)
        {
            return await _configDashboard.GetEnvironmentConfigurationStatusAsync(environment);
        }

        /// <summary>
        /// Reports a configuration issue to the health monitoring system
        /// </summary>
        public async Task ReportConfigurationIssueAsync(ConfigurationHealthReport report)
        {
            var check = new ConfigurationHealthCheck
            {
                LastCheck = report.Timestamp,
                Status = MapSeverityToStatus(report.Severity),
                Issues = new List<string> { report.Message }
            };

            _healthChecks.Add(check);
            
            // If the issue is critical, trigger immediate health check
            if (report.Severity >= WarningSeverity.Error)
            {
                await CheckHealthAsync();
            }
        }

        private ConfigurationHealthStatus MapSeverityToStatus(WarningSeverity severity)
        {
            return severity switch
            {
                WarningSeverity.Critical => ConfigurationHealthStatus.Error,
                WarningSeverity.Error => ConfigurationHealthStatus.Invalid,
                WarningSeverity.Warning => ConfigurationHealthStatus.RequiresMigration,
                _ => ConfigurationHealthStatus.Unknown
            };
        }

        private void ValidateEnvironmentSettings(object config, ConfigurationHealthCheck check)
        {
            var properties = config.GetType().GetProperties();
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            foreach (var prop in properties)
            {
                var envAttr = prop.GetCustomAttribute<EnvironmentSpecificAttribute>();
                if (envAttr != null)
                {
                    var value = prop.GetValue(config);
                    if (value != null && !string.IsNullOrEmpty(currentEnv) &&
                        !Array.Exists(envAttr.Environments, e => e.Equals(currentEnv, StringComparison.OrdinalIgnoreCase)))
                    {
                        check.Status = ConfigurationHealthStatus.Invalid;
                        check.Issues.Add(
                            $"Property {prop.Name} has environment-specific value for non-allowed environment {currentEnv}");
                    }
                }
            }
        }
    }

    public class ConfigurationHealthReport
    {
        public DateTime Timestamp { get; set; }
        public WarningSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public string AffectedComponent { get; set; }
    }

    public class SystemHealthReport
    {
        public DateTime Timestamp { get; set; }
        public List<ConfigurationHealthCheck> ConfigurationHealth { get; set; } = new();
    }

    public class ConfigurationHealthCheck
    {
        public Type ConfigurationType { get; set; }
        public DateTime LastCheck { get; set; }
        public ConfigurationHealthStatus Status { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public enum ConfigurationHealthStatus
    {
        Unknown,
        Healthy,
        Invalid,
        RequiresMigration,
        MigrationFailed,
        Error
    }
}