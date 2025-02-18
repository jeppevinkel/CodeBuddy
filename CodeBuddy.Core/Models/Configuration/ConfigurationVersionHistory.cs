using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Represents a historical record of configuration changes
    /// </summary>
    public class ConfigurationVersionHistory
    {
        /// <summary>
        /// Unique identifier for the configuration version
        /// </summary>
        public string VersionId { get; set; }

        /// <summary>
        /// Timestamp when the configuration change was made
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// User or process that made the configuration change
        /// </summary>
        public string ChangedBy { get; set; }

        /// <summary>
        /// Optional reason provided for the configuration change
        /// </summary>
        public string ChangeReason { get; set; }

        /// <summary>
        /// Migration ID if the change was part of a configuration migration
        /// </summary>
        public string MigrationId { get; set; }

        /// <summary>
        /// Dictionary of changed settings with their previous and new values
        /// </summary>
        public Dictionary<string, ConfigurationValueChange> Changes { get; set; }

        /// <summary>
        /// List of features or components affected by this change
        /// </summary>
        public List<string> AffectedComponents { get; set; }

        /// <summary>
        /// Complete configuration state at this version
        /// </summary>
        public Dictionary<string, object> ConfigurationState { get; set; }
    }

    /// <summary>
    /// Represents a change in a configuration value
    /// </summary>
    public class ConfigurationValueChange
    {
        /// <summary>
        /// Previous value before the change
        /// </summary>
        public object PreviousValue { get; set; }

        /// <summary>
        /// New value after the change
        /// </summary>
        public object NewValue { get; set; }
    }
}