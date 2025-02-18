using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation
{
    public class ConfigurationManager : IConfigurationManager
    {
        private readonly IConfigurationVersionHistoryStorage _versionHistoryStorage;
        private readonly ConfigurationTransitionValidator _transitionValidator;
        private readonly string _currentUser;
        private Dictionary<string, object> _currentConfig;

        public ConfigurationManager(
            IConfigurationVersionHistoryStorage versionHistoryStorage,
            ConfigurationTransitionValidator transitionValidator,
            string currentUser = "system")
        {
            _versionHistoryStorage = versionHistoryStorage;
            _currentUser = currentUser;
            _currentConfig = new Dictionary<string, object>();
        }

        public async Task<(bool Success, List<string> Errors)> UpdateConfigurationAsync(
            Dictionary<string, object> changes,
            string reason = null,
            string migrationId = null,
            List<string> affectedComponents = null)
        {
            try
            {
                // Create a copy of the current configuration
                var previousConfig = new Dictionary<string, object>(_currentConfig);

                // Validate the transition
                var (isValid, errors) = _transitionValidator.ValidateTransition(
                    previousConfig,
                    changes,
                    migrationId);

                if (!isValid)
                {
                    return (false, errors);
                }

                // Apply changes
                foreach (var change in changes)
                {
                    _currentConfig[change.Key] = change.Value;
                }

                // Validate referential integrity
                var (integrityValid, integrityErrors) = _transitionValidator.ValidateReferentialIntegrity(_currentConfig);
                
                if (!integrityValid)
                {
                    // Rollback changes if integrity validation fails
                    _currentConfig = previousConfig;
                    return (false, integrityErrors);
                }

                // Create version history entry
                var versionHistory = new ConfigurationVersionHistory
                {
                    VersionId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    ChangedBy = _currentUser,
                    ChangeReason = reason,
                    MigrationId = migrationId,
                    Changes = CreateChangesDictionary(previousConfig, changes),
                    AffectedComponents = affectedComponents ?? new List<string>(),
                    ConfigurationState = new Dictionary<string, object>(_currentConfig)
                };

                // Store the version history
                await _versionHistoryStorage.StoreVersionAsync(versionHistory);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetConfigurationAtTimeAsync(DateTime timestamp)
        {
            return await _versionHistoryStorage.GetConfigurationAtTimeAsync(timestamp);
        }

        public async Task<bool> RollbackToVersionAsync(string versionId)
        {
            try
            {
                var version = await _versionHistoryStorage.GetVersionAsync(versionId);
                if (version == null)
                {
                    return false;
                }

                var rollbackChanges = new Dictionary<string, object>();
                foreach (var item in version.ConfigurationState)
                {
                    rollbackChanges[item.Key] = item.Value;
                }

                return await UpdateConfigurationAsync(
                    rollbackChanges,
                    $"Rollback to version {versionId}",
                    null,
                    version.AffectedComponents);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<ConfigurationVersionHistory>> GetVersionHistoryAsync(
            DateTime startTime,
            DateTime endTime)
        {
            return await _versionHistoryStorage.ListVersionsAsync(startTime, endTime);
        }

        public async Task<Dictionary<string, ConfigurationValueChange>> CompareVersionsAsync(
            string versionId1,
            string versionId2)
        {
            return await _versionHistoryStorage.CompareVersionsAsync(versionId1, versionId2);
        }

        private Dictionary<string, ConfigurationValueChange> CreateChangesDictionary(
            Dictionary<string, object> previousConfig,
            Dictionary<string, object> changes)
        {
            var changesDictionary = new Dictionary<string, ConfigurationValueChange>();

            foreach (var change in changes)
            {
                previousConfig.TryGetValue(change.Key, out var previousValue);
                
                changesDictionary[change.Key] = new ConfigurationValueChange
                {
                    PreviousValue = previousValue,
                    NewValue = change.Value
                };
            }

            return changesDictionary;
        }
    }
}