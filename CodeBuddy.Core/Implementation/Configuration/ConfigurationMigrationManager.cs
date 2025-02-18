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

    public ConfigurationMigrationManager(
        ILogger<ConfigurationMigrationManager> logger,
        string backupPath = "config_backups",
        string migrationHistoryPath = "config_migrations.json")
    {
        _logger = logger;
        _backupPath = backupPath;
        _migrationHistoryPath = migrationHistoryPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Load migration history
        _migrationHistory = LoadMigrationHistory();
        
        // Ensure backup directory exists
        Directory.CreateDirectory(_backupPath);
    }

    /// <summary>
    /// Checks if a configuration needs migration
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
        return typeof(T).GetCustomAttribute<SchemaVersionAttribute>()?.Version;
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