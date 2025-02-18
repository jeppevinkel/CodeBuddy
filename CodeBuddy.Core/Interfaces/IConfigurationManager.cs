using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages application configuration with support for validation, migration, and secure storage
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Gets configuration for a section with automatic migration if needed
    /// </summary>
    Task<T> GetConfiguration<T>(string section) where T : class, new();
    
    /// <summary>
    /// Gets configuration with support for environment-specific overrides
    /// </summary>
    Task<T> GetConfiguration<T>(string section, string environment) where T : class, new();
    
    /// <summary>
    /// Saves configuration with validation and versioning
    /// </summary>
    Task SaveConfiguration<T>(string section, T configuration) where T : class;
    
    /// <summary>
    /// Validates configuration and returns detailed validation results
    /// </summary>
    IEnumerable<ValidationResult> ValidateConfiguration<T>(T configuration) where T : class;
    
    /// <summary>
    /// Gets configuration schema version for a section
    /// </summary>
    string GetConfigurationVersion(string section);

    /// <summary>
    /// Registers configuration change notification
    /// </summary>
    void RegisterChangeCallback<T>(string section, Action<T> callback) where T : class;

    /// <summary>
    /// Gets secure configuration value
    /// </summary>
    Task<string> GetSecureValue(string section, string key);

    /// <summary>
    /// Sets secure configuration value
    /// </summary>
    Task SetSecureValue(string section, string key, string value);

    /// <summary>
    /// Backs up configuration to specified location
    /// </summary>
    Task BackupConfiguration(string backupPath);

    /// <summary>
    /// Restores configuration from backup
    /// </summary>
    Task RestoreConfiguration(string backupPath);

    /// <summary>
    /// Gets configuration metadata for a section
    /// </summary>
    Task<IDictionary<string, string>> GetConfigurationMetadata(string section);

    /// <summary>
    /// Generates configuration documentation
    /// </summary>
    Task<string> GenerateConfigurationDocumentation();
}