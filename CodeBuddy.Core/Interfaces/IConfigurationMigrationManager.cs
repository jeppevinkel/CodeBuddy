using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.Configuration;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages configuration migrations between schema versions
/// </summary>
public interface IConfigurationMigrationManager
{
    /// <summary>
    /// Checks if a configuration needs migration
    /// </summary>
    bool NeedsMigration<T>(string section, T configuration) where T : class;
    
    /// <summary>
    /// Migrates a configuration to the latest schema version
    /// </summary>
    Task<MigrationResult> MigrateConfiguration<T>(string section, T configuration) where T : class;
    
    /// <summary>
    /// Gets migration history for a configuration section
    /// </summary>
    IEnumerable<MigrationRecord> GetMigrationHistory(string section);

    /// <summary>
    /// Validates version compatibility between configuration sections
    /// </summary>
    bool ValidateVersionCompatibility(string section, string version);

    /// <summary>
    /// Adds a version dependency between configuration sections
    /// </summary>
    void AddVersionDependency(string section, string dependentSection);
}