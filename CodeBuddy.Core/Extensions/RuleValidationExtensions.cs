using System;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Rules;

namespace CodeBuddy.Core.Extensions
{
    public static class RuleValidationExtensions
    {
        /// <summary>
        /// Validates if a rule should be activated based on the current context.
        /// </summary>
        public static bool ShouldActivateRule(this ValidationOptions options, CustomRule rule, ValidationContext context)
        {
            // Check if rule is explicitly disabled
            if (options.ConditionalRuleActivation.TryGetValue(rule.Id, out bool isEnabled) && !isEnabled)
                return false;

            // Check contextual rules
            if (options.ContextualRules.TryGetValue(rule.Id, out var requiredContext))
            {
                if (!MatchesContext(context, requiredContext))
                    return false;
            }

            // Check if rule is part of the active chain
            if (options.ChainedRules.Any() && !options.ChainedRules.Contains(rule.Id))
                return false;

            return true;
        }

        /// <summary>
        /// Validates if current context matches required context.
        /// </summary>
        private static bool MatchesContext(ValidationContext current, ValidationContext required)
        {
            // Match language
            if (!string.IsNullOrEmpty(required.Language) && 
                !string.Equals(current.Language, required.Language, StringComparison.OrdinalIgnoreCase))
                return false;

            // Match file path pattern
            if (!string.IsNullOrEmpty(required.FilePathPattern) && 
                !System.Text.RegularExpressions.Regex.IsMatch(current.FilePath, required.FilePathPattern))
                return false;

            // Match environment
            if (!string.IsNullOrEmpty(required.Environment) && 
                !string.Equals(current.Environment, required.Environment, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Resolves effective parameters for a rule based on inheritance.
        /// </summary>
        public static Dictionary<string, object> ResolveEffectiveParameters(
            this ValidationOptions options,
            string ruleId,
            Dictionary<string, object> defaultParameters)
        {
            var effectiveParams = new Dictionary<string, object>(defaultParameters);

            // Apply parameters from inherited templates in order
            foreach (var templateId in options.InheritedRuleTemplates)
            {
                if (options.RuleParameters.TryGetValue(templateId, out var templateParams))
                {
                    foreach (var param in (Dictionary<string, object>)templateParams)
                    {
                        effectiveParams[param.Key] = param.Value;
                    }
                }
            }

            // Apply rule-specific parameters last (highest precedence)
            if (options.RuleParameters.TryGetValue(ruleId, out var ruleParams))
            {
                foreach (var param in (Dictionary<string, object>)ruleParams)
                {
                    effectiveParams[param.Key] = param.Value;
                }
            }

            return effectiveParams;
        }
    }
}