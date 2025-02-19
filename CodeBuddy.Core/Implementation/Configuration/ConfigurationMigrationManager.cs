using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using Newtonsoft.Json;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration migrations between different schema versions
    /// </summary>
    public class ConfigurationMigrationManager : IConfigurationMigrationManager
    {
        private readonly string _backupPath;
        private readonly Dictionary<Type, List<IConfigurationMigration>> _migrations;
        private readonly List<MigrationRecord> _migrationHistory;

        public ConfigurationMigrationManager(string backupPath = "config/backups")
        {
            _backupPath = backupPath;
            _migrations = new Dictionary<Type, List<IConfigurationMigration>>();
            _migrationHistory = new List<MigrationRecord>();

            // Ensure backup directory exists
            Directory.CreateDirectory(_backupPath);
        }

        /// <summary>
        /// Registers a migration for a specific configuration type
        /// </summary>
        public void RegisterMigration<TConfig>(IConfigurationMigration migration)
        {
            var configType = typeof(TConfig);
            if (!_migrations.ContainsKey(configType))
            {
                _migrations[configType] = new List<IConfigurationMigration>();
            }

            _migrations[configType].Add(migration);
            // Sort migrations by version to ensure correct order
            _migrations[configType].Sort((a, b) => 
                Version.Parse(a.FromVersion).CompareTo(Version.Parse(b.FromVersion)));
        }

        /// <summary>
        /// Checks if migration is needed for the given configuration
        /// </summary>
        public bool RequiresMigration<T>(T configuration) where T : BaseConfiguration
        {
            var configType = typeof(T);
            if (!_migrations.ContainsKey(configType))
            {
                return false;
            }

            var currentVersion = configuration.SchemaVersion;
            var latestVersion = GetLatestVersion(configType);

            return currentVersion < latestVersion;
        }

        /// <summary>
        /// Migrates configuration to the latest version
        /// </summary>
        public async Task<T> MigrateAsync<T>(T configuration) where T : BaseConfiguration
        {
            var configType = typeof(T);
            if (!_migrations.ContainsKey(configType))
            {
                return configuration;
            }

            var currentVersion = configuration.SchemaVersion;
            var backupPath = await CreateBackupAsync(configuration);

            try
            {
                var migrations = GetRequiredMigrations(configType, currentVersion);
                var result = configuration;

                foreach (var migration in migrations)
                {
                    // Validate before migration
                    var validationResult = migration.Validate(result);
                    if (!validationResult.IsValid)
                    {
                        throw new ConfigurationMigrationException(
                            $"Validation failed for migration from {migration.FromVersion} to {migration.ToVersion}: " +
                            string.Join(", ", validationResult.Errors));
                    }

                    // Perform migration
                    result = (T)migration.Migrate(result);
                    result.SchemaVersion = Version.Parse(migration.ToVersion);
                    result.LastModified = DateTime.UtcNow;

                    // Record successful migration
                    _migrationHistory.Add(new MigrationRecord
                    {
                        ConfigSection = configType.Name,
                        FromVersion = migration.FromVersion,
                        ToVersion = migration.ToVersion,
                        MigrationDate = DateTime.UtcNow,
                        Success = true,
                        BackupPath = backupPath,
                        Description = $"Migrated {configType.Name} from v{migration.FromVersion} to v{migration.ToVersion}"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                // Record failed migration
                _migrationHistory.Add(new MigrationRecord
                {
                    ConfigSection = configType.Name,
                    FromVersion = currentVersion.ToString(),
                    ToVersion = GetLatestVersion(configType).ToString(),
                    MigrationDate = DateTime.UtcNow,
                    Success = false,
                    BackupPath = backupPath,
                    Description = $"Migration failed: {ex.Message}"
                });

                // Restore from backup
                await RestoreFromBackupAsync<T>(backupPath);
                throw;
            }
        }

        /// <summary>
        /// Gets the migration history for a configuration type
        /// </summary>
        public IEnumerable<MigrationRecord> GetMigrationHistory<T>() where T : BaseConfiguration
        {
            var configType = typeof(T).Name;
            return _migrationHistory
                .Where(m => m.ConfigSection == configType)
                .OrderByDescending(m => m.MigrationDate);
        }

        private Version GetLatestVersion(Type configType)
        {
            if (!_migrations.ContainsKey(configType))
            {
                return new Version(1, 0);
            }

            var lastMigration = _migrations[configType].Last();
            return Version.Parse(lastMigration.ToVersion);
        }

        private List<IConfigurationMigration> GetRequiredMigrations(Type configType, Version currentVersion)
        {
            return _migrations[configType]
                .Where(m => Version.Parse(m.FromVersion) >= currentVersion 
                           && Version.Parse(m.ToVersion) > currentVersion)
                .OrderBy(m => Version.Parse(m.FromVersion))
                .ToList();
        }

        private async Task<string> CreateBackupAsync<T>(T configuration) where T : BaseConfiguration
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var backupFileName = $"{typeof(T).Name}_v{configuration.SchemaVersion}_{timestamp}.json";
            var backupPath = Path.Combine(_backupPath, backupFileName);

            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            await File.WriteAllTextAsync(backupPath, json);

            return backupPath;
        }

        private async Task<T> RestoreFromBackupAsync<T>(string backupPath) where T : BaseConfiguration
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            var json = await File.ReadAllTextAsync(backupPath);
            return JsonConvert.DeserializeObject<T>(json) 
                   ?? throw new InvalidOperationException("Failed to restore configuration from backup");
        }
    }

    public class ConfigurationMigrationException : Exception
    {
        public ConfigurationMigrationException(string message) : base(message) { }
        public ConfigurationMigrationException(string message, Exception inner) : base(message, inner) { }
    }
}