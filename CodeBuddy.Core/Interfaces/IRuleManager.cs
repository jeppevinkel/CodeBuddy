using System.Collections.Generic;
using CodeBuddy.Core.Models.Rules;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for managing custom validation rules.
    /// </summary>
    public interface IRuleManager
    {
        /// <summary>
        /// Registers a new custom rule or updates an existing one.
        /// </summary>
        void RegisterRule(CustomRule rule);

        /// <summary>
        /// Gets a rule by its ID.
        /// </summary>
        CustomRule GetRule(string ruleId);

        /// <summary>
        /// Gets all registered rules.
        /// </summary>
        IEnumerable<CustomRule> GetAllRules();

        /// <summary>
        /// Gets rules applicable to a specific programming language.
        /// </summary>
        IEnumerable<CustomRule> GetRulesForLanguage(string language);

        /// <summary>
        /// Returns rules in dependency-resolved, priority-ordered sequence.
        /// </summary>
        IEnumerable<CustomRule> GetOrderedRules();
    }
}