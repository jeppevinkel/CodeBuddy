using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for managing application configuration with version history support
    /// </summary>
    public interface IConfigurationManager
    {
        /// <summary>
        /// Updates the configuration with the specified changes and records the change history
        /// </summary>
        /// <param name="changes">Dictionary of configuration changes to apply</param>
        /// <param name="reason">Optional reason for the changes</param>
        /// <param name="migrationId">Optional migration ID if changes are part of a migration</param>
        /// <param name="affectedComponents">Optional list of components affected by the changes</param>
        /// <returns>True if changes were applied successfully, false otherwise</returns>
        Task<(bool Success, List<string> Errors)> UpdateConfigurationAsync(
            Dictionary<string, object> changes,
            string reason = null,
            string migrationId = null,
            List<string> affectedComponents = null);

        /// <summary>
        /// Retrieves the configuration state at a specific point in time
        /// </summary>
        /// <param name="timestamp">The timestamp to retrieve the configuration for</param>
        /// <returns>The configuration state at that time</returns>
        Task<Dictionary<string, object>> GetConfigurationAtTimeAsync(DateTime timestamp);

        /// <summary>
        /// Rolls back the configuration to a specific version
        /// </summary>
        /// <param name="versionId">The version ID to roll back to</param>
        /// <returns>True if rollback was successful, false otherwise</returns>
        Task<bool> RollbackToVersionAsync(string versionId);

        /// <summary>
        /// Retrieves the configuration version history within a specified time range
        /// </summary>
        /// <param name="startTime">Start of the time range</param>
        /// <param name="endTime">End of the time range</param>
        /// <returns>List of configuration version history entries</returns>
        Task<List<ConfigurationVersionHistory>> GetVersionHistoryAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Compares two configuration versions and returns the differences
        /// </summary>
        /// <param name="versionId1">First version ID to compare</param>
        /// <param name="versionId2">Second version ID to compare</param>
        /// <returns>Dictionary of changes between the versions</returns>
        Task<Dictionary<string, ConfigurationValueChange>> CompareVersionsAsync(string versionId1, string versionId2);
    }
}