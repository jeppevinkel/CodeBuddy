using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration;

/// <summary>
/// Manages configuration migrations and version tracking
/// </summary>
public class ConfigurationMigrationManager : IConfigurationMigrationManager
{
    private readonly ILogger<ConfigurationMigrationManager> _logger;
    private readonly string _backupPath;
    private readonly string _migrationHistoryPath;
    private readonly List<MigrationRecord> _migrationHistory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, List<string>> _versionDependencies;

    public ConfigurationMigrationManager(
        ILogger<ConfigurationMigrationManager> logger,
        string backupPath = "config_backups",
        string migrationHistoryPath = "config_migrations.json",
        string versionDependenciesPath = "config_dependencies.json")
    {
        _logger = logger;
        _backupPath = backupPath;
        _migrationHistoryPath = migrationHistoryPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Load migration history and version dependencies
        _migrationHistory = LoadMigrationHistory();
        _versionDependencies = LoadVersionDependencies(versionDependenciesPath);
        
        // Ensure backup directory exists
        Directory.CreateDirectory(_backupPath);
    }

    /// <summary>
    /// Validates the version compatibility across related configuration sections
    /// </summary>
    public bool ValidateVersionCompatibility(string section, string version)
    {
        if (!_versionDependencies.TryGetValue(section, out var dependencies))
        {
            return true;
        }

        foreach (var dependency in dependencies)
        {
            var dependencyHistory = GetMigrationHistory(dependency).FirstOrDefault();
            if (dependencyHistory == null)
            {
                continue;
            }

            if (!IsVersionCompatible(version, dependencyHistory.ToVersion))
            {
                _logger.LogError("Version {Version} of section {Section} is incompatible with version {DependencyVersion} of {Dependency}",
                    version, section, dependencyHistory.ToVersion, dependency);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if two versions are compatible
    /// </summary>
    private bool IsVersionCompatible(string version1, string version2)
    {
        var v1 = Version.Parse(version1);
        var v2 = Version.Parse(version2);

        // Major version must match for compatibility
        return v1.Major == v2.Major;
    }

    /// <summary>
    /// Tracks dependencies between configuration sections
    /// </summary>
    public void AddVersionDependency(string section, string dependentSection)
    {
        if (!_versionDependencies.ContainsKey(section))
        {
            _versionDependencies[section] = new List<string>();
        }

        if (!_versionDependencies[section].Contains(dependentSection))
        {
            _versionDependencies[section].Add(dependentSection);
            SaveVersionDependencies();
        }
    }

    /// <summary>
    /// Checks if configuration is compatible with current schema version
    /// </summary>
    public bool NeedsMigration<T>(string section, T configuration) where T : class
    {
        var currentVersion = GetConfigurationVersion<T>();
        var migratorType = GetMigratorType<T>();
        
        if (migratorType == null || currentVersion == null)
        {
            return false;
        }

        var migrator = CreateMigrator(migratorType);
        return Version.Parse(migrator.FromVersion) < Version.Parse(currentVersion);
    }

    /// <summary>
    /// Performs a configuration migration
    /// </summary>
    public async Task<MigrationResult> MigrateConfiguration<T>(string section, T configuration) where T : class
    {
        var schemaVersion = GetConfigurationVersion<T>();
        if (schemaVersion == null)
        {
            return MigrationResult.Failed<T>("Configuration class must be decorated with SchemaVersionAttribute");
        }

        if (!ValidateVersionCompatibility(section, schemaVersion))
        {
            return MigrationResult.Failed<T>("Configuration version is incompatible with dependent sections");
        }

        return await PerformMigration(section, configuration);
    }

    /// <summary>
    /// Performs the actual migration process
    /// </summary>
    private async Task<MigrationResult> PerformMigration<T>(string section, T configuration) where T : class
    {
        var migratorType = GetMigratorType<T>();
        if (migratorType == null)
        {
            return MigrationResult.Success(configuration);
        }

        try
        {
            // Create backup
            var backupPath = await CreateBackup(section, configuration);
            
            // Perform migration
            var migrator = CreateMigrator(migratorType);
            var migrated = migrator.Migrate(configuration);
            
            // Validate result
            var validationResult = migrator.Validate(migrated);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Migration validation failed for section {Section}. Errors: {Errors}",
                    section, string.Join(", ", validationResult.Errors));
                
                // Restore from backup
                var restored = await RestoreFromBackup<T>(backupPath);
                return MigrationResult.Failed<T>(
                    validationResult.Errors.FirstOrDefault() ?? "Migration validation failed",
                    restored);
            }

            // Record successful migration
            RecordMigration(new MigrationRecord
            {
                ConfigSection = section,
                FromVersion = migrator.FromVersion,
                ToVersion = migrator.ToVersion,
                MigrationDate = DateTime.UtcNow,
                Success = true,
                BackupPath = backupPath
            });

            return MigrationResult.Success((T)migrated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating configuration for section {Section}", section);
            return MigrationResult.Failed<T>($"Migration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets migration history for a section
    /// </summary>
    public IEnumerable<MigrationRecord> GetMigrationHistory(string section)
    {
        return _migrationHistory
            .Where(r => r.ConfigSection == section)
            .OrderByDescending(r => r.MigrationDate);
    }

    /// <summary>
    /// Creates a backup of the configuration
    /// </summary>
    private async Task<string> CreateBackup<T>(string section, T configuration) where T : class
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(_backupPath, $"{section}_{timestamp}.json");
        
        await File.WriteAllTextAsync(backupPath, 
            JsonSerializer.Serialize(configuration, _jsonOptions));
        
        return backupPath;
    }

    /// <summary>
    /// Restores configuration from a backup file
    /// </summary>
    private async Task<T?> RestoreFromBackup<T>(string backupPath) where T : class
    {
        if (!File.Exists(backupPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(backupPath);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    /// <summary>
    /// Records a migration in the history
    /// </summary>
    private void RecordMigration(MigrationRecord record)
    {
        // Validate version sequence integrity
        var previousMigration = GetMigrationHistory(record.ConfigSection).FirstOrDefault();
        if (previousMigration != null)
        {
            if (Version.Parse(record.FromVersion) != Version.Parse(previousMigration.ToVersion))
            {
                throw new InvalidOperationException(
                    $"Invalid migration sequence. Expected migration from version {previousMigration.ToVersion} but got {record.FromVersion}");
            }
        }

        _migrationHistory.Add(record);
        SaveMigrationHistory();
    }

    /// <summary>
    /// Loads the migration history from disk
    /// </summary>
    private List<MigrationRecord> LoadMigrationHistory()
    {
        if (!File.Exists(_migrationHistoryPath))
        {
            return new List<MigrationRecord>();
        }

        try
        {
            var json = File.ReadAllText(_migrationHistoryPath);
            return JsonSerializer.Deserialize<List<MigrationRecord>>(json, _jsonOptions) 
                   ?? new List<MigrationRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading migration history");
            return new List<MigrationRecord>();
        }
    }

    /// <summary>
    /// Saves the migration history to disk
    /// </summary>
    private void SaveMigrationHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_migrationHistory, _jsonOptions);
            File.WriteAllText(_migrationHistoryPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving migration history");
        }
    }

    /// <summary>
    /// Gets the schema version from a configuration type
    /// </summary>
    private static string? GetConfigurationVersion<T>() where T : class
    {
        var attribute = typeof(T).GetCustomAttribute<SchemaVersionAttribute>();
        if (attribute == null)
        {
            return null;
        }

        if (!IsValidSemVer(attribute.Version))
        {
            throw new InvalidOperationException($"Invalid semantic version format: {attribute.Version}");
        }

        return attribute.Version;
    }

    /// <summary>
    /// Validates semantic version format
    /// </summary>
    private static bool IsValidSemVer(string version)
    {
        try
        {
            return Version.TryParse(version, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads version dependencies from disk
    /// </summary>
    private Dictionary<string, List<string>> LoadVersionDependencies(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, List<string>>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, _jsonOptions)
                   ?? new Dictionary<string, List<string>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading version dependencies");
            return new Dictionary<string, List<string>>();
        }
    }

    /// <summary>
    /// Saves version dependencies to disk
    /// </summary>
    private void SaveVersionDependencies()
    {
        try
        {
            var json = JsonSerializer.Serialize(_versionDependencies, _jsonOptions);
            File.WriteAllText(_migrationHistoryPath.Replace("migrations", "dependencies"), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving version dependencies");
        }
    }

    /// <summary>
    /// Gets the migrator type for a configuration type
    /// </summary>
    private static Type? GetMigratorType<T>() where T : class
    {
        return typeof(T).GetCustomAttribute<RequiresMigrationAttribute>()?.MigratorType;
    }

    /// <summary>
    /// Creates a migrator instance
    /// </summary>
    private static IConfigurationMigration CreateMigrator(Type migratorType)
    {
        return (IConfigurationMigration)Activator.CreateInstance(migratorType)!;
    }
}

/// <summary>
/// Result of a migration operation
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? Configuration { get; set; }

    public static MigrationResult Success<T>(T configuration) where T : class => new()
    {
        Success = true,
        Configuration = configuration
    };

    public static MigrationResult Failed<T>(string error, T? configuration = null) where T : class => new()
    {
        Success = false,
        Error = error,
        Configuration = configuration
    };
}