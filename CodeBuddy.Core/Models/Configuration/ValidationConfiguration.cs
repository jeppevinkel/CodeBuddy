using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Represents the configuration for validation components including monitoring settings
    /// </summary>
    public class ValidationConfiguration
    {
        [Required]
        public string Name { get; set; }
        
        public MonitoringConfiguration Monitoring { get; set; } = new MonitoringConfiguration();
        
        public ValidationSettings ValidationSettings { get; set; } = new ValidationSettings();
        
        public AlertConfiguration AlertSettings { get; set; } = new AlertConfiguration();
    }

    public class MonitoringConfiguration
    {
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public int MetricsCollectionIntervalSeconds { get; set; } = 60;
        public int RetentionPeriodDays { get; set; } = 30;
        public bool EnableHistoricalTracking { get; set; } = true;
    }

    public class ValidationSettings
    {
        public bool ValidateOnChange { get; set; } = true;
        public bool EnforceSchemaVersion { get; set; } = true;
        public bool AutomaticallyApplyMigrations { get; set; } = false;
        public List<string> ExcludedValidations { get; set; } = new List<string>();
    }

    public class AlertConfiguration
    {
        public bool EnableAlerts { get; set; } = true;
        public List<string> AlertChannels { get; set; } = new List<string> { "Email" };
        public int AlertThrottleMinutes { get; set; } = 15;
        public WarningSeverity MinimumAlertSeverity { get; set; } = WarningSeverity.Warning;
    }

    public enum WarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    public enum WarningType
    {
        DeprecatedConfiguration,
        RequiredMigration,
        InvalidConfiguration,
        ResourceLimitViolation,
        SchemaVersionMismatch
    }

    public class ConfigurationWarning
    {
        public WarningType Type { get; set; }
        public string Message { get; set; }
        public WarningSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ConfigurationHealthStatus
    {
        public DateTime Timestamp { get; set; }
        public List<ValidationResult> ValidationResults { get; set; } = new List<ValidationResult>();
        public List<SchemaVersionStatus> SchemaVersionCompliance { get; set; } = new List<SchemaVersionStatus>();
        public MigrationStatus MigrationStatus { get; set; }
        public Dictionary<string, PluginConfigurationState> PluginConfigurationStates { get; set; }
    }

    public class SchemaVersionStatus
    {
        public string ConfigurationName { get; set; }
        public string CurrentVersion { get; set; }
        public string ExpectedVersion { get; set; }
        public bool IsCompliant { get; set; }
    }

    public class MigrationStatus
    {
        public int PendingMigrations { get; set; }
        public DateTime LastSuccessfulMigration { get; set; }
        public bool HasFailedMigrations { get; set; }
        public List<MigrationHistoryEntry> RecentMigrations { get; set; } = new List<MigrationHistoryEntry>();
    }

    public class MigrationHistoryEntry
    {
        public string MigrationId { get; set; }
        public DateTime AppliedAt { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PluginConfigurationState
    {
        public bool IsConfigured { get; set; }
        public ValidationResult ConfigurationStatus { get; set; }
        public DateTime LastValidated { get; set; }
    }

    public class ComponentValidationStatus
    {
        public string ComponentName { get; set; }
        public ValidationResult ValidationResults { get; set; }
        public DateTime LastValidated { get; set; }
    }

    public class EnvironmentConfigurationStatus
    {
        public string Environment { get; set; }
        public object ConfigurationSnapshot { get; set; }
        public ValidationResult ValidationStatus { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}