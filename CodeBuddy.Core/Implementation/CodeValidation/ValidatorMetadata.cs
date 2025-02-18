using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Metadata for code validators including version and capabilities information
    /// </summary>
    public class ValidatorMetadata
    {
        /// <summary>
        /// The version of the validator
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// The name of the validator provider/author
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Description of the validator
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// List of supported validator capabilities
        /// </summary>
        public HashSet<string> Capabilities { get; set; } = new HashSet<string>();

        /// <summary>
        /// Minimum required runtime version
        /// </summary>
        public Version MinimumRuntimeVersion { get; set; }

        /// <summary>
        /// List of dependencies required by this validator
        /// </summary>
        public IList<ValidatorDependency> Dependencies { get; set; } = new List<ValidatorDependency>();

        /// <summary>
        /// Configuration settings for the validator
        /// </summary>
        public IDictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Creation timestamp of the validator
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp of the validator
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indicates if this validator can be hot-reloaded
        /// </summary>
        public bool SupportsHotReload { get; set; }

        /// <summary>
        /// Priority level for this validator (higher numbers take precedence)
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Represents a dependency required by a validator
    /// </summary>
    public class ValidatorDependency
    {
        /// <summary>
        /// Name of the dependency
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Version requirement for the dependency
        /// </summary>
        public string VersionRequirement { get; set; }

        /// <summary>
        /// Indicates if this is an optional dependency
        /// </summary>
        public bool IsOptional { get; set; }
    }
}