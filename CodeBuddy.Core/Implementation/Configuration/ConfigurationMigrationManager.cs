using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    private readonly int _maxBackupCount = 10;
    private readonly string _environment;
    private ConfigurationSnapshot? _currentSnapshot;

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
    /// Performs the actual migration process with enhanced validation and error recovery
    /// </summary>
    private async Task<MigrationResult> PerformMigration<T>(string section, T configuration) where T : class
    {
        var migratorType = GetMigratorType<T>();
        if (migratorType == null)
        {
            return MigrationResult.Success(configuration);
        }

        // Detect circular dependencies
        if (HasCircularDependencies(section))
        {
            return MigrationResult.Failed<T>("Circular dependency detected in configuration relationships");
        }

        // Create pre-migration snapshot and validation context
        _currentSnapshot = await CreateConfigurationSnapshot();
        var context = new PreMigrationContext
        {
            CurrentConfigurations = _currentSnapshot.Configurations,
            TargetVersions = _currentSnapshot.Versions,
            Dependencies = _versionDependencies
        };

        // Create detailed diagnostic context for error reporting
        var diagnostics = new Dictionary<string, object>
        {
            ["ConfigSection"] = section,
            ["MigrationTime"] = DateTime.UtcNow,
            ["Environment"] = _environment,
            ["Dependencies"] = GetDependencyVersions(section),
            ["PreMigrationState"] = configuration
        };

        try
        {
            // Pre-migration validation
            var migrator = CreateMigrator(migratorType);
            var preValidation = migrator.Validate(configuration);
            if (!preValidation.IsValid)
            {
                diagnostics["PreValidationErrors"] = preValidation.Errors;
                _logger.LogError("Pre-migration validation failed for section {Section}. Errors: {Errors}",
                    section, string.Join(", ", preValidation.Errors));
                return MigrationResult.Failed<T>("Pre-migration validation failed", configuration);
            }

            // Create backup with compression and metadata
            var backupPath = await CreateBackup(section, configuration);
            diagnostics["BackupPath"] = backupPath;
            
            // Perform migration
            var migrated = migrator.Migrate(configuration);
            diagnostics["PostMigrationState"] = migrated;
            
            // Post-migration validation
            var postValidation = migrator.Validate(migrated);
            if (!postValidation.IsValid)
            {
                diagnostics["PostValidationErrors"] = postValidation.Errors;
                _logger.LogError("Post-migration validation failed for section {Section}. Errors: {Errors}",
                    section, string.Join(", ", postValidation.Errors));
                
                // Atomic rollback using snapshot
                foreach (var (key, value) in _currentSnapshot!.Configurations)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(_backupPath, $"{key}_current.json"),
                        JsonSerializer.Serialize(value, _jsonOptions));
                }
                
                return MigrationResult.Failed<T>(
                    postValidation.Errors.FirstOrDefault() ?? "Post-migration validation failed",
                    configuration);
            }

            // Cross-reference validation
            var dependencyValidation = ValidateDependencyIntegrity(section, migrator.ToVersion);
            if (!dependencyValidation.IsValid)
            {
                diagnostics["DependencyValidationErrors"] = dependencyValidation.Errors;
                _logger.LogError("Dependency validation failed for section {Section}. Errors: {Errors}",
                    section, string.Join(", ", dependencyValidation.Errors));
                return MigrationResult.Failed<T>("Dependency validation failed", configuration);
            }

            // Record successful migration with detailed diagnostics
            var record = new MigrationRecord
            {
                ConfigSection = section,
                FromVersion = migrator.FromVersion,
                ToVersion = migrator.ToVersion,
                MigrationDate = DateTime.UtcNow,
                Success = true,
                BackupPath = backupPath,
                Description = JsonSerializer.Serialize(diagnostics, _jsonOptions)
            };
            
            RecordMigration(record);
            return MigrationResult.Success((T)migrated);
        }
        catch (Exception ex)
        {
            diagnostics["Error"] = new
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.Message
            };

            _logger.LogError(ex, "Error migrating configuration for section {Section}. Diagnostics: {Diagnostics}", 
                section, JsonSerializer.Serialize(diagnostics, _jsonOptions));

            // Ensure atomic rollback on error
            if (_currentSnapshot != null)
            {
                try
                {
                    foreach (var (key, value) in _currentSnapshot.Configurations)
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(_backupPath, $"{key}_current.json"),
                            JsonSerializer.Serialize(value, _jsonOptions));
                    }
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error during rollback for section {Section}", section);
                }
            }

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
    /// Creates a backup of the configuration with compression and metadata
    /// </summary>
    private async Task<string> CreateBackup<T>(string section, T configuration) where T : class
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var baseFileName = $"{section}_{timestamp}";
        var jsonPath = Path.Combine(_backupPath, $"{baseFileName}.json");
        var zipPath = Path.Combine(_backupPath, $"{baseFileName}.zip");
        var metadataPath = Path.Combine(_backupPath, $"{baseFileName}_metadata.json");

        // Serialize configuration
        var configJson = JsonSerializer.Serialize(configuration, _jsonOptions);
        await File.WriteAllTextAsync(jsonPath, configJson);

        // Create zip archive
        using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zipArchive.CreateEntryFromFile(jsonPath, Path.GetFileName(jsonPath));
        }

        // Create and save metadata
        var metadata = new BackupMetadata
        {
            BackupPath = zipPath,
            CreatedAt = DateTime.UtcNow,
            Environment = _environment,
            Dependencies = GetDependencyVersions(section),
            IsCompressed = true,
            OriginalSize = new FileInfo(jsonPath).Length,
            CompressedSize = new FileInfo(zipPath).Length
        };

        await File.WriteAllTextAsync(metadataPath,
            JsonSerializer.Serialize(metadata, _jsonOptions));

        // Cleanup uncompressed file
        File.Delete(jsonPath);

        // Enforce backup rotation
        await EnforceBackupRotation(section);

        return zipPath;
    }

    /// <summary>
    /// Creates a complete snapshot of current configuration state
    /// </summary>
    private async Task<ConfigurationSnapshot> CreateConfigurationSnapshot()
    {
        var snapshot = new ConfigurationSnapshot
        {
            CreatedAt = DateTime.UtcNow,
            Description = "Pre-migration snapshot"
        };

        foreach (var section in _versionDependencies.Keys)
        {
            var configPath = Path.Combine(_backupPath, $"{section}_current.json");
            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                snapshot.Configurations[section] = JsonSerializer.Deserialize<object>(json, _jsonOptions)!;
                snapshot.Versions[section] = GetCurrentVersion(section);
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Gets the current version for a configuration section
    /// </summary>
    private string GetCurrentVersion(string section)
    {
        var history = GetMigrationHistory(section).FirstOrDefault();
        return history?.ToVersion ?? "1.0.0";
    }

    /// <summary>
    /// Gets all dependency versions for a section
    /// </summary>
    private Dictionary<string, string> GetDependencyVersions(string section)
    {
        var versions = new Dictionary<string, string>();
        if (_versionDependencies.TryGetValue(section, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                versions[dependency] = GetCurrentVersion(dependency);
            }
        }
        return versions;
    }

    /// <summary>
    /// Enforces backup rotation policy
    /// </summary>
    private async Task EnforceBackupRotation(string section)
    {
        var backups = Directory.GetFiles(_backupPath, $"{section}_*.zip")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Skip(_maxBackupCount);

        foreach (var backup in backups)
        {
            try
            {
                File.Delete(backup);
                var metadataFile = backup.Replace(".zip", "_metadata.json");
                if (File.Exists(metadataFile))
                {
                    File.Delete(metadataFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up old backup: {Path}", backup);
            }
        }
    }

    /// <summary>
    /// Detects circular dependencies in configuration relationships
    /// </summary>
    private bool HasCircularDependencies(string section, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        
        if (!_versionDependencies.TryGetValue(section, out var dependencies))
        {
            return false;
        }

        if (!visited.Add(section))
        {
            return true; // Circular dependency detected
        }

        foreach (var dependency in dependencies)
        {
            if (HasCircularDependencies(dependency, visited))
            {
                return true;
            }
        }

        visited.Remove(section);
        return false;
    }

    /// <summary>
    /// Validates version sequence integrity with enhanced checks
    /// </summary>
    private void ValidateVersionSequence(MigrationRecord record)
    {
        var previousMigration = GetMigrationHistory(record.ConfigSection).FirstOrDefault();
        if (previousMigration != null)
        {
            var fromVersion = Version.Parse(record.FromVersion);
            var expectedVersion = Version.Parse(previousMigration.ToVersion);
            
            if (fromVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid migration sequence. Expected migration from version {expectedVersion} but got {fromVersion}");
            }

            // Validate version increment
            var toVersion = Version.Parse(record.ToVersion);
            if (toVersion <= fromVersion)
            {
                throw new InvalidOperationException(
                    $"Invalid version increment. New version {toVersion} must be greater than {fromVersion}");
            }
        }
    }

    /// <summary>
    /// Validates integrity of dependent configurations
    /// </summary>
    private ValidationResult ValidateDependencyIntegrity(string section, string targetVersion)
    {
        var errors = new List<string>();
        
        if (_versionDependencies.TryGetValue(section, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                var dependencyVersion = GetCurrentVersion(dependency);
                if (!IsVersionCompatible(targetVersion, dependencyVersion))
                {
                    errors.Add($"Incompatible dependency version: {dependency} at version {dependencyVersion}");
                }

                // Check for breaking changes in dependency
                var dependencyHistory = GetMigrationHistory(dependency)
                    .Where(m => Version.Parse(m.ToVersion) > Version.Parse(dependencyVersion))
                    .ToList();

                if (dependencyHistory.Any(m => HasBreakingChanges(m)))
                {
                    errors.Add($"Breaking changes detected in dependency {dependency}");
                }
            }
        }

        return errors.Any() 
            ? ValidationResult.Failed(errors.ToArray()) 
            : ValidationResult.Success();
    }

    /// <summary>
    /// Checks if a migration includes breaking changes
    /// </summary>
    private bool HasBreakingChanges(MigrationRecord migration)
    {
        var fromVersion = Version.Parse(migration.FromVersion);
        var toVersion = Version.Parse(migration.ToVersion);
        return toVersion.Major > fromVersion.Major;
    }

    /// <summary>
    /// Gets metadata for a backup file
    /// </summary>
    private BackupMetadata? GetBackupMetadata(string? backupPath)
    {
        if (string.IsNullOrEmpty(backupPath))
            return null;

        var metadataPath = backupPath.Replace(".zip", "_metadata.json");
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<BackupMetadata>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading backup metadata: {Path}", metadataPath);
            return null;
        }
    }

    /// <summary>
    /// Restores configuration from a backup file with compression support
    /// </summary>
    private async Task<T?> RestoreFromBackup<T>(string backupPath) where T : class
    {
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            if (backupPath.EndsWith(".zip"))
            {
                using var archive = ZipFile.OpenRead(backupPath);
                var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".json"));
                if (entry == null)
                    return null;

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            else
            {
                var json = await File.ReadAllTextAsync(backupPath);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring from backup: {Path}", backupPath);
            return null;
        }
    }

    /// <summary>
    /// Records a migration in the history
    /// </summary>
    private void RecordMigration(MigrationRecord record)
    {
        ValidateVersionSequence(record);
        _migrationHistory.Add(record);
        SaveMigrationHistory();
        
        // Create migration health report
        var report = new
        {
            Timestamp = DateTime.UtcNow,
            Section = record.ConfigSection,
            FromVersion = record.FromVersion,
            ToVersion = record.ToVersion,
            Success = record.Success,
            BackupInfo = GetBackupMetadata(record.BackupPath),
            Dependencies = GetDependencyVersions(record.ConfigSection),
            VersionSequence = GetMigrationHistory(record.ConfigSection)
                .Take(5)
                .Select(m => new { m.FromVersion, m.ToVersion, m.MigrationDate })
                .ToList()
        };

        var reportPath = Path.Combine(_backupPath, "health_reports", 
            $"migration_report_{record.ConfigSection}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, _jsonOptions));
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