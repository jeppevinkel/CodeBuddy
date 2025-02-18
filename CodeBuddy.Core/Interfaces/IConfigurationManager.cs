using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Handles application configuration and settings with migration support
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Gets configuration for a section with automatic migration if needed
    /// </summary>
    Task<T> GetConfiguration<T>(string section) where T : class, new();
    
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
}