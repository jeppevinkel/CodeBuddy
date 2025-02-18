using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Base class for all configuration sections
    /// </summary>
    public abstract class BaseConfiguration : IValidatableObject
    {
        /// <summary>
        /// Configuration section identifier
        /// </summary>
        public string SectionId { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Configuration schema version
        /// </summary>
        public Version SchemaVersion { get; set; }

        /// <summary>
        /// Environment this configuration is for
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Metadata about this configuration section
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Basic validation
            if (string.IsNullOrEmpty(SectionId))
            {
                results.Add(new ValidationResult("SectionId is required", new[] { nameof(SectionId) }));
            }

            if (SchemaVersion == null)
            {
                results.Add(new ValidationResult("SchemaVersion is required", new[] { nameof(SchemaVersion) }));
            }

            return results;
        }
    }

    /// <summary>
    /// Base class for feature-specific configuration sections
    /// </summary>
    public abstract class FeatureConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Whether the feature is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Feature-specific settings
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Dependencies on other features
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var result in base.Validate(validationContext))
            {
                yield return result;
            }

            // Validate dependencies
            if (Enabled && Dependencies.Count > 0)
            {
                // Dependency validation would be implemented here
                // This would check if all required features are enabled
            }
        }
    }

    /// <summary>
    /// Base class for plugin configuration sections
    /// </summary>
    public abstract class PluginConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Plugin identifier
        /// </summary>
        public string PluginId { get; set; }

        /// <summary>
        /// Plugin version
        /// </summary>
        public Version PluginVersion { get; set; }

        /// <summary>
        /// Plugin-specific settings
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var result in base.Validate(validationContext))
            {
                yield return result;
            }

            if (string.IsNullOrEmpty(PluginId))
            {
                yield return new ValidationResult("PluginId is required", new[] { nameof(PluginId) });
            }

            if (PluginVersion == null)
            {
                yield return new ValidationResult("PluginVersion is required", new[] { nameof(PluginVersion) });
            }
        }
    }
}