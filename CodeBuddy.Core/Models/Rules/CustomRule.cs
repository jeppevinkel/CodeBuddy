using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Rules
{
    /// <summary>
    /// Represents a custom validation rule with metadata and configuration.
    /// </summary>
    public class CustomRule
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Version of the rule
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// Priority of the rule (lower numbers are processed first)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// IDs of rules that must be executed before this rule
        /// </summary>
        public HashSet<string> Dependencies { get; set; }

        /// <summary>
        /// JSON Schema defining the rule's configuration structure
        /// </summary>
        public string ConfigurationSchema { get; set; }

        /// <summary>
        /// Supported programming languages for this rule
        /// </summary>
        public HashSet<string> SupportedLanguages { get; set; }

        /// <summary>
        /// Estimated performance impact (1-10, where 10 is highest impact)
        /// </summary>
        public int PerformanceImpact { get; set; }

        /// <summary>
        /// Human-readable description of the rule
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Detailed documentation in markdown format
        /// </summary>
        public string Documentation { get; set; }

        /// <summary>
        /// Rule configuration data
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; }

        public CustomRule()
        {
            Dependencies = new HashSet<string>();
            SupportedLanguages = new HashSet<string>();
            Configuration = new Dictionary<string, object>();
        }
    }
}