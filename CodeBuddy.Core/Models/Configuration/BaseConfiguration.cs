using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Base class for all configuration sections
    /// </summary>
    public abstract class BaseConfiguration
    {
        /// <summary>
        /// Gets or sets whether this configuration section is enabled
        /// </summary>
        [ConfigurationItem("Controls whether this configuration section is enabled", required: true, defaultValue: "true")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets custom configuration metadata
        /// </summary>
        [ConfigurationItem("Custom metadata for this configuration section", required: false)]
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when this configuration was last modified
        /// </summary>
        [ConfigurationItem("When this configuration was last modified", required: false)]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets who last modified this configuration
        /// </summary>
        [ConfigurationItem("Who last modified this configuration", required: false)]
        public string LastModifiedBy { get; set; } = string.Empty;

        /// <summary>
        /// Validates the configuration and returns any validation errors
        /// </summary>
        public virtual IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            // Base validation logic
            if (!Enabled && Metadata.Count > 0)
            {
                errors.Add("Metadata should not be set when configuration is disabled");
            }

            if (LastModified > DateTime.UtcNow)
            {
                errors.Add("LastModified cannot be in the future");
            }

            return errors;
        }
    }
}