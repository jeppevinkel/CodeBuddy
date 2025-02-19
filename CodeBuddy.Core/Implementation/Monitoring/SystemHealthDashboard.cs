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

        public SystemHealthDashboard(
            ConfigurationValidationManager validationManager,
            ConfigurationMigrationManager migrationManager)
        {
            _validationManager = validationManager;
            _migrationManager = migrationManager;
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