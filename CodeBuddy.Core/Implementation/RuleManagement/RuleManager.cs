using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CodeBuddy.Core.Models.Rules;

namespace CodeBuddy.Core.Implementation.RuleManagement
{
    /// <summary>
    /// Manages the registration, validation, and execution of custom validation rules.
    /// </summary>
    public class RuleManager : IRuleManager
    {
        private readonly Dictionary<string, CustomRule> _rules;
        private bool _isRuleOrderStale;
        private List<CustomRule> _sortedRules;

        public RuleManager()
        {
            _rules = new Dictionary<string, CustomRule>();
            _isRuleOrderStale = true;
            _sortedRules = new List<CustomRule>();
        }

        /// <summary>
        /// Registers a new custom rule or updates an existing one.
        /// </summary>
        /// <param name="rule">The rule to register</param>
        /// <exception cref="ArgumentException">Thrown when rule validation fails</exception>
        public void RegisterRule(CustomRule rule)
        {
            ValidateRule(rule);
            
            if (_rules.ContainsKey(rule.Id))
            {
                var existingRule = _rules[rule.Id];
                if (existingRule.Version >= rule.Version)
                {
                    throw new ArgumentException($"Rule {rule.Id} version {rule.Version} is not newer than existing version {existingRule.Version}");
                }
            }

            _rules[rule.Id] = rule;
            _isRuleOrderStale = true;
        }

        /// <summary>
        /// Validates a rule's configuration and dependencies.
        /// </summary>
        private void ValidateRule(CustomRule rule)
        {
            if (string.IsNullOrEmpty(rule.Id))
                throw new ArgumentException("Rule ID cannot be empty");

            if (rule.Version == null)
                throw new ArgumentException("Rule version must be specified");

            if (rule.Priority < 0)
                throw new ArgumentException("Rule priority cannot be negative");

            if (string.IsNullOrEmpty(rule.ConfigurationSchema))
                throw new ArgumentException("Configuration schema must be specified");

            // Validate JSON schema format
            try
            {
                JsonDocument.Parse(rule.ConfigurationSchema);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid configuration schema format", ex);
            }

            if (rule.SupportedLanguages.Count == 0)
                throw new ArgumentException("Rule must support at least one language");

            if (rule.PerformanceImpact < 1 || rule.PerformanceImpact > 10)
                throw new ArgumentException("Performance impact must be between 1 and 10");
        }

        /// <summary>
        /// Checks for circular dependencies in the rule set.
        /// </summary>
        private void DetectCircularDependencies()
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var rule in _rules.Values)
            {
                if (!visited.Contains(rule.Id))
                {
                    if (HasCircularDependency(rule.Id, visited, recursionStack))
                        throw new InvalidOperationException($"Circular dependency detected starting from rule {rule.Id}");
                }
            }
        }

        private bool HasCircularDependency(string ruleId, HashSet<string> visited, HashSet<string> recursionStack)
        {
            visited.Add(ruleId);
            recursionStack.Add(ruleId);

            var rule = _rules[ruleId];
            foreach (var depId in rule.Dependencies)
            {
                if (!_rules.ContainsKey(depId))
                    throw new InvalidOperationException($"Missing dependency: {depId} required by {ruleId}");

                if (!visited.Contains(depId))
                {
                    if (HasCircularDependency(depId, visited, recursionStack))
                        return true;
                }
                else if (recursionStack.Contains(depId))
                {
                    return true;
                }
            }

            recursionStack.Remove(ruleId);
            return false;
        }

        /// <summary>
        /// Returns rules in dependency-resolved, priority-ordered sequence.
        /// </summary>
        public IEnumerable<CustomRule> GetOrderedRules()
        {
            if (!_isRuleOrderStale)
                return _sortedRules;

            DetectCircularDependencies();
            _sortedRules = TopologicalSort().ToList();
            _isRuleOrderStale = false;
            return _sortedRules;
        }

        private IEnumerable<CustomRule> TopologicalSort()
        {
            var visited = new HashSet<string>();
            var ordered = new List<CustomRule>();

            foreach (var rule in _rules.Values.OrderBy(r => r.Priority))
            {
                if (!visited.Contains(rule.Id))
                    Visit(rule.Id, visited, ordered);
            }

            return ordered;
        }

        private void Visit(string ruleId, HashSet<string> visited, List<CustomRule> ordered)
        {
            visited.Add(ruleId);
            var rule = _rules[ruleId];

            foreach (var depId in rule.Dependencies.OrderBy(id => _rules[id].Priority))
            {
                if (!visited.Contains(depId))
                    Visit(depId, visited, ordered);
            }

            ordered.Add(rule);
        }

        /// <summary>
        /// Gets a rule by its ID.
        /// </summary>
        public CustomRule GetRule(string ruleId)
        {
            return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
        }

        /// <summary>
        /// Gets all registered rules.
        /// </summary>
        public IEnumerable<CustomRule> GetAllRules()
        {
            return _rules.Values;
        }

        /// <summary>
        /// Gets rules applicable to a specific programming language.
        /// </summary>
        public IEnumerable<CustomRule> GetRulesForLanguage(string language)
        {
            return _rules.Values.Where(r => r.SupportedLanguages.Contains(language));
        }
    }
}