using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public interface IConfigurationDashboard
    {
        Task<List<ConfigurationHealthStatus>> GetConfigurationHealthStatusAsync();
        Task<List<ConfigurationChangeEvent>> GetConfigurationChangeHistoryAsync();
        Task<ConfigurationPerformanceMetrics> GetPerformanceMetricsAsync();
        Task<List<ConfigurationUsageAnalytics>> GetUsageAnalyticsAsync();
        Task<ConfigurationMigrationStats> GetMigrationStatsAsync();
        Task TrackConfigurationAccessAsync(string section);
        Task AlertOnValidationFailureAsync(ConfigurationValidationIssue issue);
    }

    public class ConfigurationDashboard : IConfigurationDashboard
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationValidator _validator;
        private readonly IConfigurationMigrationManager _migrationManager;
        private readonly ILoggingService _logger;
        private readonly Dictionary<string, ConfigurationHealthStatus> _healthStatusCache;
        private readonly List<ConfigurationChangeEvent> _changeHistory;

        public ConfigurationDashboard(
            IConfigurationManager configManager,
            IConfigurationValidator validator,
            IConfigurationMigrationManager migrationManager,
            ILoggingService logger)
        {
            _configManager = configManager;
            _validator = validator;
            _migrationManager = migrationManager;
            _logger = logger;
            _healthStatusCache = new Dictionary<string, ConfigurationHealthStatus>();
            _changeHistory = new List<ConfigurationChangeEvent>();
        }

        public async Task<List<ConfigurationHealthStatus>> GetConfigurationHealthStatusAsync()
        {
            try
            {
                var components = await _configManager.GetAllComponentsAsync();
                var statuses = new List<ConfigurationHealthStatus>();

                foreach (var component in components)
                {
                    var config = await _configManager.GetConfigurationAsync(component);
                    var validationResult = await _validator.ValidateConfigurationAsync(config);
                    
                    var status = new ConfigurationHealthStatus
                    {
                        ComponentName = component,
                        IsHealthy = validationResult.IsValid,
                        Environment = config.Environment,
                        Version = config.Version,
                        ValidationIssues = validationResult.Issues,
                        LastChecked = DateTime.UtcNow
                    };

                    _healthStatusCache[component] = status;
                    statuses.Add(status);
                }

                return statuses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting configuration health status: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ConfigurationChangeEvent>> GetConfigurationChangeHistoryAsync()
        {
            return _changeHistory.OrderByDescending(x => x.ChangedAt).ToList();
        }

        public async Task<ConfigurationPerformanceMetrics> GetPerformanceMetricsAsync()
        {
            try
            {
                var metrics = new ConfigurationPerformanceMetrics
                {
                    LoadTimeMs = await _configManager.GetAverageLoadTimeAsync(),
                    ValidationTimeMs = await _validator.GetAverageValidationTimeAsync(),
                    CacheHitRate = await _configManager.GetCacheHitRateAsync(),
                    MeasuredAt = DateTime.UtcNow
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting performance metrics: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ConfigurationUsageAnalytics>> GetUsageAnalyticsAsync()
        {
            try
            {
                var analytics = await _configManager.GetUsageAnalyticsAsync();
                return analytics.Select(a => new ConfigurationUsageAnalytics
                {
                    Section = a.Key,
                    AccessCount = a.Value.AccessCount,
                    LastAccessed = a.Value.LastAccessed,
                    AccessPatterns = a.Value.AccessPatterns
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting usage analytics: {ex.Message}");
                throw;
            }
        }

        public async Task<ConfigurationMigrationStats> GetMigrationStatsAsync()
        {
            try
            {
                return await _migrationManager.GetMigrationStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting migration stats: {ex.Message}");
                throw;
            }
        }

        public async Task TrackConfigurationAccessAsync(string section)
        {
            try
            {
                await _configManager.TrackConfigurationAccessAsync(section);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error tracking configuration access: {ex.Message}");
            }
        }

        public async Task AlertOnValidationFailureAsync(ConfigurationValidationIssue issue)
        {
            try
            {
                _logger.LogWarning($"Configuration validation issue detected: {issue.Message} in section {issue.Section}");
                
                var changeEvent = new ConfigurationChangeEvent
                {
                    Section = issue.Section,
                    ChangedAt = issue.DetectedAt,
                    ChangedBy = "System",
                    NewValue = "Invalid Configuration",
                    OldValue = "Unknown"
                };

                _changeHistory.Add(changeEvent);
                
                // TODO: Implement alert notification system
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing validation failure alert: {ex.Message}");
            }
        }
    }
}