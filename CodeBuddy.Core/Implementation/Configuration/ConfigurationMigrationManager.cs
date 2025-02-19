using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using CodeBuddy.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration schema migrations and version upgrades
    /// </summary>
    public class ConfigurationMigrationManager : IConfigurationMigrationManager
    {
        private readonly ILogger _logger;
        private readonly string _migrationsPath;
        private readonly List<MigrationRecord> _migrationHistory = new();

        public ConfigurationMigrationManager(ILogger logger)
        {
            _logger = logger;
            _migrationsPath = Path.Combine(AppContext.BaseDirectory, "config_migrations");
            LoadMigrationHistory();
        }

        public bool NeedsMigration(string section, object configuration)
        {
            var configType = configuration.GetType();
            var currentVersion = GetCurrentVersion(configType);
            var requiredVersion = GetRequiredVersion(configType);

            return currentVersion < requiredVersion;
        }

        public async Task<MigrationResult> MigrateConfiguration(string section, object configuration)
        {
            try
            {
                var configType = configuration.GetType();
                var currentVersion = GetCurrentVersion(configType);
                var targetVersion = GetRequiredVersion(configType);

                if (currentVersion >= targetVersion)
                {
                    return MigrationResult.Success(configuration);
                }

                _logger.LogInformation(
                    "Starting configuration migration for section {Section} from v{Current} to v{Target}",
                    section, currentVersion, targetVersion);

                // Create backup
                var backupPath = await CreateBackup(section, configuration);

                // Get migration path
                var migrations = GetMigrationPath(configType, currentVersion, targetVersion);
                var migratedConfig = configuration;

                foreach (var migration in migrations)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Applying migration {From} -> {To} for section {Section}",
                            migration.FromVersion, migration.ToVersion, section);

                        migratedConfig = migration.Migrate(migratedConfig);
                        
                        var validationResult = migration.Validate(migratedConfig);
                        if (!validationResult.IsValid)
                        {
                            throw new ValidationException(
                                $"Migration validation failed: {string.Join(", ", validationResult.Errors)}");
                        }

                        // Record successful migration
                        _migrationHistory.Add(new MigrationRecord
                        {
                            ConfigSection = section,
                            FromVersion = migration.FromVersion,
                            ToVersion = migration.ToVersion,
                            MigrationDate = DateTime.UtcNow,
                            Success = true,
                            BackupPath = backupPath
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error during migration {From} -> {To} for section {Section}",
                            migration.FromVersion, migration.ToVersion, section);

                        // Record failed migration
                        _migrationHistory.Add(new MigrationRecord
                        {
                            ConfigSection = section,
                            FromVersion = migration.FromVersion,
                            ToVersion = migration.ToVersion,
                            MigrationDate = DateTime.UtcNow,
                            Success = false,
                            BackupPath = backupPath,
                            Description = ex.Message
                        });

                        return MigrationResult.Failed(ex.Message);
                    }
                }

                await SaveMigrationHistory();
                return MigrationResult.Success(migratedConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration migration for section {Section}", section);
                return MigrationResult.Failed(ex.Message);
            }
        }

        private async Task<string> CreateBackup(string section, object configuration)
        {
            var backupDir = Path.Combine(_migrationsPath, "backups");
            Directory.CreateDirectory(backupDir);

            var backupPath = Path.Combine(backupDir, 
                $"{section}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");

            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(backupPath, json);
            return backupPath;
        }

        private void LoadMigrationHistory()
        {
            var historyPath = Path.Combine(_migrationsPath, "migration_history.json");
            if (File.Exists(historyPath))
            {
                try
                {
                    var json = File.ReadAllText(historyPath);
                    var history = JsonSerializer.Deserialize<List<MigrationRecord>>(json);
                    if (history != null)
                    {
                        _migrationHistory.AddRange(history);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading migration history");
                }
            }
        }

        private async Task SaveMigrationHistory()
        {
            var historyPath = Path.Combine(_migrationsPath, "migration_history.json");
            Directory.CreateDirectory(_migrationsPath);

            var json = JsonSerializer.Serialize(_migrationHistory, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(historyPath, json);
        }

        private static Version GetCurrentVersion(Type configType)
        {
            var versionAttr = configType.GetCustomAttribute<SchemaVersionAttribute>();
            return versionAttr?.Version ?? new Version(1, 0);
        }

        private static Version GetRequiredVersion(Type configType)
        {
            var migrationAttr = configType.GetCustomAttribute<RequiresMigrationAttribute>();
            if (migrationAttr != null)
            {
                var migration = (IConfigurationMigration)Activator.CreateInstance(migrationAttr.MigratorType)!;
                return Version.Parse(migration.ToVersion);
            }
            return new Version(1, 0);
        }

        private static IConfigurationMigration[] GetMigrationPath(Type configType, Version from, Version to)
        {
            var migrations = new List<IConfigurationMigration>();
            var current = from;

            while (current < to)
            {
                var nextMigration = FindNextMigration(configType, current);
                if (nextMigration == null)
                {
                    throw new InvalidOperationException(
                        $"No migration path found from v{current} to v{to}");
                }

                migrations.Add(nextMigration);
                current = Version.Parse(nextMigration.ToVersion);
            }

            return migrations.ToArray();
        }

        private static IConfigurationMigration? FindNextMigration(Type configType, Version current)
        {
            var migrationAttr = configType.GetCustomAttribute<RequiresMigrationAttribute>();
            if (migrationAttr == null) return null;

            var migration = (IConfigurationMigration)Activator.CreateInstance(migrationAttr.MigratorType)!;
            if (Version.Parse(migration.FromVersion) == current)
            {
                return migration;
            }

            return null;
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public object? Configuration { get; set; }

        public static MigrationResult Success(object configuration) => new()
        {
            Success = true,
            Configuration = configuration
        };

        public static MigrationResult Failed(string error) => new()
        {
            Success = false,
            Error = error
        };
    }
}