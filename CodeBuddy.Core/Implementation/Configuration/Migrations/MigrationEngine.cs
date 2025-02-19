using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration.Migrations
{
    /// <summary>
    /// Handles the execution and tracking of configuration migrations
    /// </summary>
    public class MigrationEngine
    {
        private readonly ILogger _logger;
        private readonly string _migrationHistoryPath;
        private readonly ConfigurationBackupManager _backupManager;
        private readonly Dictionary<Type, List<IMigrationScript>> _migrations;
        private readonly Dictionary<string, MigrationHistory> _migrationHistory;

        public MigrationEngine(
            ILogger logger,
            string migrationHistoryPath,
            ConfigurationBackupManager backupManager)
        {
            _logger = logger;
            _migrationHistoryPath = migrationHistoryPath;
            _backupManager = backupManager;
            _migrations = new Dictionary<Type, List<IMigrationScript>>();
            _migrationHistory = LoadMigrationHistory();
        }

        public void RegisterMigration<T>(IMigrationScript<T> migration) where T : BaseConfiguration
        {
            var type = typeof(T);
            if (!_migrations.ContainsKey(type))
            {
                _migrations[type] = new List<IMigrationScript>();
            }

            _migrations[type].Add(migration);
        }

        public async Task<MigrationResult> ExecuteMigrationsAsync<T>(T configuration, string section) 
            where T : BaseConfiguration
        {
            var type = typeof(T);
            if (!_migrations.ContainsKey(type))
            {
                return new MigrationResult { Success = true, Configuration = configuration };
            }

            try
            {
                // Create backup before migration
                var backupPath = await _backupManager.CreateBackupAsync($"pre_migration_{section}");
                _logger.LogInformation("Created pre-migration backup at {BackupPath}", backupPath);

                var currentVersion = configuration.SchemaVersion;
                var targetVersion = GetLatestVersion(type);

                if (currentVersion >= targetVersion)
                {
                    return new MigrationResult { Success = true, Configuration = configuration };
                }

                var migrationPath = CalculateMigrationPath(type, currentVersion, targetVersion);
                if (!migrationPath.Any())
                {
                    return new MigrationResult 
                    { 
                        Success = false, 
                        Error = $"No migration path found from {currentVersion} to {targetVersion}" 
                    };
                }

                var currentConfig = configuration;
                foreach (var migration in migrationPath)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Executing migration from {FromVersion} to {ToVersion} for {Section}",
                            migration.FromVersion, migration.ToVersion, section);

                        // Execute migration
                        currentConfig = await migration.ExecuteAsync(currentConfig);

                        // Validate migrated configuration
                        var validationResult = await ValidateMigratedConfiguration(currentConfig);
                        if (!validationResult.Success)
                        {
                            throw new Exception($"Migration validation failed: {validationResult.Error}");
                        }

                        // Record successful migration
                        await RecordMigration(section, migration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "Error executing migration from {FromVersion} to {ToVersion} for {Section}",
                            migration.FromVersion, migration.ToVersion, section);

                        // Restore from backup
                        await _backupManager.RestoreFromBackupAsync(backupPath);
                        
                        return new MigrationResult 
                        { 
                            Success = false, 
                            Error = $"Migration failed: {ex.Message}",
                            Configuration = configuration // Return original configuration
                        };
                    }
                }

                return new MigrationResult { Success = true, Configuration = currentConfig };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during migration process for {Section}", section);
                return new MigrationResult 
                { 
                    Success = false, 
                    Error = $"Migration process failed: {ex.Message}",
                    Configuration = configuration
                };
            }
        }

        private List<IMigrationScript> CalculateMigrationPath(Type configType, Version fromVersion, Version toVersion)
        {
            var availableMigrations = _migrations[configType]
                .OrderBy(m => m.FromVersion)
                .ToList();

            var path = new List<IMigrationScript>();
            var current = fromVersion;

            while (current < toVersion)
            {
                var nextMigration = availableMigrations
                    .FirstOrDefault(m => m.FromVersion == current && m.ToVersion <= toVersion);

                if (nextMigration == null)
                {
                    // No valid migration found
                    return new List<IMigrationScript>();
                }

                path.Add(nextMigration);
                current = nextMigration.ToVersion;
            }

            return path;
        }

        private Version GetLatestVersion(Type configType)
        {
            return _migrations[configType]
                .Max(m => m.ToVersion);
        }

        private async Task<ValidationResult> ValidateMigratedConfiguration<T>(T configuration) 
            where T : BaseConfiguration
        {
            try
            {
                // Basic validation
                var validationResult = configuration.Validate();
                if (validationResult != null && validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                {
                    return new ValidationResult { Success = false, Error = validationResult.ErrorMessage };
                }

                // Schema validation
                var schemaAttribute = typeof(T).GetCustomAttribute<SchemaVersionAttribute>();
                if (schemaAttribute != null && configuration.SchemaVersion > schemaAttribute.Version)
                {
                    return new ValidationResult 
                    { 
                        Success = false, 
                        Error = $"Configuration version {configuration.SchemaVersion} exceeds schema version {schemaAttribute.Version}" 
                    };
                }

                return new ValidationResult { Success = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult { Success = false, Error = $"Validation error: {ex.Message}" };
            }
        }

        private Dictionary<string, MigrationHistory> LoadMigrationHistory()
        {
            try
            {
                if (File.Exists(_migrationHistoryPath))
                {
                    var json = File.ReadAllText(_migrationHistoryPath);
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, MigrationHistory>>(json)
                        ?? new Dictionary<string, MigrationHistory>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading migration history");
            }

            return new Dictionary<string, MigrationHistory>();
        }

        private async Task RecordMigration(string section, IMigrationScript migration)
        {
            if (!_migrationHistory.ContainsKey(section))
            {
                _migrationHistory[section] = new MigrationHistory();
            }

            _migrationHistory[section].Migrations.Add(new MigrationRecord
            {
                FromVersion = migration.FromVersion.ToString(),
                ToVersion = migration.ToVersion.ToString(),
                ExecutedAt = DateTime.UtcNow
            });

            await SaveMigrationHistory();
        }

        private async Task SaveMigrationHistory()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_migrationHistory, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_migrationHistoryPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving migration history");
            }
        }
    }

    public class MigrationHistory
    {
        public List<MigrationRecord> Migrations { get; set; } = new();
    }

    public class MigrationRecord
    {
        public string FromVersion { get; set; } = "";
        public string ToVersion { get; set; } = "";
        public DateTime ExecutedAt { get; set; }
    }

    public class ValidationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public interface IMigrationScript
    {
        Version FromVersion { get; }
        Version ToVersion { get; }
        Task<object> ExecuteAsync(object configuration);
    }

    public interface IMigrationScript<T> : IMigrationScript where T : BaseConfiguration
    {
        Task<T> ExecuteMigrationAsync(T configuration);

        async Task<object> IMigrationScript.ExecuteAsync(object configuration)
        {
            if (configuration is T typedConfig)
            {
                return await ExecuteMigrationAsync(typedConfig);
            }
            throw new ArgumentException($"Configuration is not of type {typeof(T).Name}");
        }
    }
}