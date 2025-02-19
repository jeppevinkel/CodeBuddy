using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationMigrationManager : IConfigurationMigrationManager
    {
        private readonly Dictionary<string, IConfigurationMigration> _migrations;
        private readonly IConfigurationValidator _validator;

        public ConfigurationMigrationManager(IConfigurationValidator validator)
        {
            _migrations = new Dictionary<string, IConfigurationMigration>();
            _validator = validator;
        }

        public void RegisterMigration(IConfigurationMigration migration)
        {
            var key = $"{migration.FromVersion}-{migration.ToVersion}";
            _migrations[key] = migration;
        }

        public async Task<T> MigrateIfNeeded<T>(T config, string section, string currentVersion) where T : class
        {
            var targetVersion = GetTargetVersion<T>();
            if (currentVersion == targetVersion)
            {
                return config;
            }

            var migrationPath = FindMigrationPath(currentVersion, targetVersion);
            if (!migrationPath.Any())
            {
                throw new InvalidOperationException(
                    $"No migration path found from version {currentVersion} to {targetVersion}");
            }

            var result = config;
            foreach (var migration in migrationPath)
            {
                result = await migration.Migrate(result);
                
                // Validate after each migration step
                var validationResults = _validator.Validate(result);
                if (validationResults.Any())
                {
                    throw new InvalidOperationException(
                        $"Configuration validation failed after migrating from {migration.FromVersion} to {migration.ToVersion}: " +
                        string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
                }
            }

            return result;
        }

        private string GetTargetVersion<T>()
        {
            var attr = typeof(T).GetCustomAttributes(typeof(SchemaVersionAttribute), true)
                .FirstOrDefault() as SchemaVersionAttribute;
            
            return attr?.Version ?? "1.0";
        }

        private List<IConfigurationMigration> FindMigrationPath(string fromVersion, string toVersion)
        {
            var path = new List<IConfigurationMigration>();
            var visited = new HashSet<string>();
            
            if (FindPath(fromVersion, toVersion, visited, path))
            {
                return path;
            }
            
            return new List<IConfigurationMigration>();
        }

        private bool FindPath(string currentVersion, string targetVersion, 
            HashSet<string> visited, List<IConfigurationMigration> path)
        {
            if (currentVersion == targetVersion)
            {
                return true;
            }

            visited.Add(currentVersion);

            var possibleMigrations = _migrations.Values
                .Where(m => m.FromVersion == currentVersion && !visited.Contains(m.ToVersion));

            foreach (var migration in possibleMigrations)
            {
                path.Add(migration);
                if (FindPath(migration.ToVersion, targetVersion, visited, path))
                {
                    return true;
                }
                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    public interface IConfigurationMigration
    {
        string FromVersion { get; }
        string ToVersion { get; }
        Task<T> Migrate<T>(T config) where T : class;
    }
}