using System;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Base class for all configuration classes with common validation and versioning
    /// </summary>
    public abstract class BaseConfiguration
    {
        /// <summary>
        /// Configuration schema version
        /// </summary>
        [Required]
        public Version SchemaVersion { get; set; } = new Version(1, 0);

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Environment this configuration is for
        /// </summary>
        [EnvironmentSpecific("Development", "Staging", "Production")]
        public string? Environment { get; set; }

        /// <summary>
        /// Whether this configuration can be modified at runtime
        /// </summary>
        [Reloadable]
        public bool IsReloadable { get; set; }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public virtual ValidationResult? Validate()
        {
            if (SchemaVersion == null)
            {
                return new ValidationResult("Schema version is required");
            }

            if (LastModified == default)
            {
                return new ValidationResult("Last modified timestamp is required");
            }

            return ValidationResult.Success;
        }
    }
}