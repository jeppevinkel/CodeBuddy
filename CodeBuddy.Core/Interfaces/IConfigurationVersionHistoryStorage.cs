using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for storing and retrieving configuration version history
    /// </summary>
    public interface IConfigurationVersionHistoryStorage
    {
        /// <summary>
        /// Stores a new configuration version history entry
        /// </summary>
        /// <param name="versionHistory">The version history entry to store</param>
        Task StoreVersionAsync(ConfigurationVersionHistory versionHistory);

        /// <summary>
        /// Retrieves the configuration state at a specific point in time
        /// </summary>
        /// <param name="timestamp">The timestamp to retrieve the configuration for</param>
        /// <returns>The configuration state at that time</returns>
        Task<Dictionary<string, object>> GetConfigurationAtTimeAsync(DateTime timestamp);

        /// <summary>
        /// Retrieves a specific version of the configuration by version ID
        /// </summary>
        /// <param name="versionId">The version ID to retrieve</param>
        /// <returns>The configuration version history entry</returns>
        Task<ConfigurationVersionHistory> GetVersionAsync(string versionId);

        /// <summary>
        /// Lists all configuration versions within a time range
        /// </summary>
        /// <param name="startTime">Start of the time range</param>
        /// <param name="endTime">End of the time range</param>
        /// <returns>List of configuration version history entries</returns>
        Task<List<ConfigurationVersionHistory>> ListVersionsAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Compares two configuration versions and returns the differences
        /// </summary>
        /// <param name="versionId1">First version ID to compare</param>
        /// <param name="versionId2">Second version ID to compare</param>
        /// <returns>Dictionary of changes between the versions</returns>
        Task<Dictionary<string, ConfigurationValueChange>> CompareVersionsAsync(string versionId1, string versionId2);

        /// <summary>
        /// Cleans up old configuration history entries based on retention policy
        /// </summary>
        /// <param name="retentionDays">Number of days to retain history</param>
        Task CleanupHistoryAsync(int retentionDays);
    }
}