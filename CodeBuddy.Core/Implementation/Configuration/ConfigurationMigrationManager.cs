using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationMigrationManager : IConfigurationMigrationManager
    {
        private readonly IConfigurationManager _configManager;
        private readonly ConfigurationTransitionValidator _transitionValidator;
        private readonly Dictionary<string, IMigration> _pendingMigrations;
        
        public ConfigurationMigrationManager(
            IConfigurationManager configManager,
            ConfigurationTransitionValidator transitionValidator)
        {
            _configManager = configManager;
            _transitionValidator = transitionValidator;
            _pendingMigrations = new Dictionary<string, IMigration>();
        }

        public void RegisterMigration(string migrationId, IMigration migration)
        {
            if (!_pendingMigrations.ContainsKey(migrationId))
            {
                _pendingMigrations.Add(migrationId, migration);
            }
        }

        public async Task<(bool Success, List<string> Errors)> ApplyMigrationAsync(
            string migrationId,
            Dictionary<string, object> currentConfig)
        {
            if (!_pendingMigrations.TryGetValue(migrationId, out var migration))
            {
                return (false, new List<string> { $"Migration {migrationId} not found" });
            }

            try
            {
                // Get the proposed changes from the migration
                var changes = await migration.GetChangesAsync(currentConfig);

                // Validate the transition
                var (isValid, errors) = _transitionValidator.ValidateTransition(
                    currentConfig,
                    changes,
                    migrationId);

                if (!isValid)
                {
                    return (false, errors);
                }

                // Apply the changes through the configuration manager to maintain history
                var updated = await _configManager.UpdateConfigurationAsync(
                    changes,
                    migration.Description,
                    migrationId,
                    migration.AffectedComponents);

                if (!updated)
                {
                    return (false, new List<string> { "Failed to apply configuration changes" });
                }

                // Validate referential integrity after the change
                var newConfig = await _configManager.GetConfigurationAtTimeAsync(DateTime.UtcNow);
                var (integrityValid, integrityErrors) = _transitionValidator.ValidateReferentialIntegrity(newConfig);

                if (!integrityValid)
                {
                    // Rollback if integrity validation fails
                    await _configManager.RollbackToVersionAsync(
                        (await _configManager.GetVersionHistoryAsync(
                            DateTime.UtcNow.AddSeconds(-1),
                            DateTime.UtcNow))[0].VersionId);
                    
                    return (false, integrityErrors);
                }

                _pendingMigrations.Remove(migrationId);
                return (true, new List<string>());
            }
            catch (Exception ex)
            {
                return (false, new List<string> { $"Migration failed: {ex.Message}" });
            }
        }

        public async Task<List<string>> GetPendingMigrationsAsync()
        {
            return new List<string>(_pendingMigrations.Keys);
        }
    }

    public interface IMigration
    {
        string Description { get; }
        List<string> AffectedComponents { get; }
        Task<Dictionary<string, object>> GetChangesAsync(Dictionary<string, object> currentConfig);
    }
}