using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration;

/// <summary>
/// Represents a configuration migration step
/// </summary>
public abstract class ConfigurationMigration<T> : IConfigurationMigration where T : class
{
    public string FromVersion { get; }
    public string ToVersion { get; }
    public string Description { get; }
    
    protected ConfigurationMigration(string fromVersion, string toVersion, string description)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Description = description;
    }

    /// <summary>
    /// Migrates configuration data from the old version to the new version
    /// </summary>
    public abstract T Migrate(T configuration);
    
    /// <summary>
    /// Validates the configuration after migration
    /// </summary>
    public abstract MigrationValidationResult Validate(T configuration);

    object IConfigurationMigration.Migrate(object configuration) => Migrate((T)configuration);
    MigrationValidationResult IConfigurationMigration.Validate(object configuration) => Validate((T)configuration);
}

/// <summary>
/// Interface for configuration migrations
/// </summary>
public interface IConfigurationMigration
{
    string FromVersion { get; }
    string ToVersion { get; }
    string Description { get; }
    object Migrate(object configuration);
    MigrationValidationResult Validate(object configuration);
}

/// <summary>
/// Result of a migration validation
/// </summary>
public class MigrationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static MigrationValidationResult Success() => new() { IsValid = true };
    
    public static MigrationValidationResult Failed(params string[] errors) => new()
    {
        IsValid = false,
        Errors = new List<string>(errors)
    };
}

/// <summary>
/// Represents a migration record
/// </summary>
public class MigrationRecord
{
    public string ConfigSection { get; set; } = "";
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public DateTime MigrationDate { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string BackupPath { get; set; } = "";
}

/// <summary>
/// Specifies that a configuration class requires the given migrator type
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequiresMigrationAttribute : Attribute
{
    public Type MigratorType { get; }

    public RequiresMigrationAttribute(Type migratorType)
    {
        if (!typeof(IConfigurationMigration).IsAssignableFrom(migratorType))
        {
            throw new ArgumentException($"Type {migratorType} must implement IConfigurationMigration");
        }
        MigratorType = migratorType;
    }
}