using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CodeBuddy.Core.Models.Rules
{
    /// <summary>
    /// Represents different types of changes that can be made to a rule
    /// </summary>
    public enum RuleChangeType
    {
        Created,
        Updated,
        Deprecated,
        Deleted
    }

    /// <summary>
    /// Tracks changes made to a rule
    /// </summary>
    public class RuleChange
    {
        public DateTime Timestamp { get; set; }
        public RuleChangeType Type { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
    }

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

        /// <summary>
        /// List of changes made to the rule
        /// </summary>
        public List<RuleChange> ChangeHistory { get; set; }

        /// <summary>
        /// Template ID if this rule was created from a template
        /// </summary>
        public string TemplateId { get; set; }

        /// <summary>
        /// Parent rule ID if this rule inherits from another
        /// </summary>
        public string ParentRuleId { get; set; }

        /// <summary>
        /// Rule complexity score (1-10)
        /// </summary>
        public int Complexity { get; set; }

        /// <summary>
        /// Whether the rule is marked as deprecated
        /// </summary>
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// ID of the rule that replaces this one if deprecated
        /// </summary>
        public string ReplacedByRuleId { get; set; }

        public CustomRule()
        {
            Dependencies = new HashSet<string>();
            SupportedLanguages = new HashSet<string>();
            Configuration = new Dictionary<string, object>();
            ChangeHistory = new List<RuleChange>();
        }

        /// <summary>
        /// Creates a deep copy of the rule.
        /// </summary>
        public CustomRule Clone()
        {
            var json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<CustomRule>(json);
        }

        /// <summary>
        /// Records a change to the rule's history.
        /// </summary>
        public void RecordChange(RuleChangeType type, string description, string author)
        {
            ChangeHistory.Add(new RuleChange
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Description = description,
                Author = author
            });
        }

        /// <summary>
        /// Marks the rule as deprecated and optionally specifies a replacement.
        /// </summary>
        public void Deprecate(string replacementRuleId = null, string reason = null)
        {
            IsDeprecated = true;
            ReplacedByRuleId = replacementRuleId;
            RecordChange(RuleChangeType.Deprecated, reason ?? "Rule marked as deprecated", "System");
        }
    }
}