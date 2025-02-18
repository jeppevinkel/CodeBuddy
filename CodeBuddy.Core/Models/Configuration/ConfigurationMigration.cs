using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Snapshot of configuration state for rollback purposes
    /// </summary>
    public class ConfigurationSnapshot
    {
        public Dictionary<string, object> Configurations { get; set; } = new();
        public Dictionary<string, string> Versions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Details about a configuration backup
    /// </summary>
    public class BackupMetadata
    {
        public string BackupPath { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Environment { get; set; } = "";
        public Dictionary<string, string> Dependencies { get; set; } = new();
        public bool IsCompressed { get; set; }
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
    }

    /// <summary>
    /// Pre-migration validation context
    /// </summary>
    public class PreMigrationContext
    {
        public Dictionary<string, object> CurrentConfigurations { get; set; } = new();
        public Dictionary<string, string> TargetVersions { get; set; } = new();
        public Dictionary<string, List<string>> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Record of a configuration migration
    /// </summary>
    public class MigrationRecord
    {
        public string ConfigSection { get; set; } = "";
        public string FromVersion { get; set; } = "";
        public string ToVersion { get; set; } = "";
        public DateTime MigrationDate { get; set; }
        public bool Success { get; set; }
        public string? BackupPath { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Validation result for configuration migration
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failed(params string[] errors) => new()
        {
            IsValid = false,
            Errors = { errors }
        };
    }

    /// <summary>
    /// Interface for configuration migration implementations
    /// </summary>
    public interface IConfigurationMigration
    {
        string FromVersion { get; }
        string ToVersion { get; }
        object Migrate(object configuration);
        ValidationResult Validate(object configuration);
    }

    /// <summary>
    /// Attribute to specify required migration for a configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class RequiresMigrationAttribute : Attribute
    {
        public Type MigratorType { get; }

        public RequiresMigrationAttribute(Type migratorType)
        {
            if (!typeof(IConfigurationMigration).IsAssignableFrom(migratorType))
            {
                throw new ArgumentException(
                    $"Migrator type must implement {nameof(IConfigurationMigration)}", 
                    nameof(migratorType));
            }

            MigratorType = migratorType;
        }
    }
}