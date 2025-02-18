using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Configuration for the validation system
    /// </summary>
    [ConfigurationSection("Validation", "Configuration for the validation system", version: 1)]
    public class ValidationConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of validation errors to report
        /// </summary>
        [ConfigurationItem("Maximum number of validation errors to report", required: true, defaultValue: "100")]
        [RangeValidation(1, 1000)]
        public int MaxErrorCount { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to enable detailed validation messages
        /// </summary>
        [ConfigurationItem("Whether to enable detailed validation messages", required: true, defaultValue: "true")]
        public bool DetailedMessages { get; set; } = true;

        /// <summary>
        /// Gets or sets the validation cache size in MB
        /// </summary>
        [ConfigurationItem("Validation cache size in MB", required: true, defaultValue: "256")]
        [RangeValidation(1, 1024)]
        public int CacheSizeMB { get; set; } = 256;

        /// <summary>
        /// Gets or sets validation severity levels for different rule types
        /// </summary>
        [ConfigurationItem("Validation severity levels for different rule types", required: true)]
        public Dictionary<string, ValidationSeverity> RuleSeverityLevels { get; set; } = new()
        {
            { "Style", ValidationSeverity.Information },
            { "Convention", ValidationSeverity.Warning },
            { "Error", ValidationSeverity.Error }
        };

        /// <summary>
        /// Gets or sets paths to exclude from validation
        /// </summary>
        [ConfigurationItem("Paths to exclude from validation")]
        [PatternValidation(@"^[^<>:""\\|?*]*$", "Invalid path pattern")]
        public List<string> ExcludePaths { get; set; } = new();

        /// <summary>
        /// Gets or sets custom validation rules
        /// </summary>
        [ConfigurationItem("Custom validation rules")]
        [CustomValidation(typeof(CustomRuleValidator))]
        public Dictionary<string, object> CustomRules { get; set; } = new();

        /// <summary>
        /// Override base validation to add custom validation logic
        /// </summary>
        public override IEnumerable<string> Validate()
        {
            var errors = new List<string>(base.Validate());

            // Custom validation logic
            if (CacheSizeMB > 512 && !DetailedMessages)
            {
                errors.Add("Large cache size requires detailed messages to be enabled");
            }

            if (MaxErrorCount > 500 && CacheSizeMB < 128)
            {
                errors.Add("High error count requires larger cache size");
            }

            foreach (var path in ExcludePaths)
            {
                if (path.Length > 260)
                {
                    errors.Add($"Path too long: {path}");
                }
            }

            return errors;
        }
    }

    /// <summary>
    /// Custom validator for validation rules
    /// </summary>
    public class CustomRuleValidator : IConfigurationValidator
    {
        public Task<ValidationResult> ValidateAsync(object value, ValidationContext context)
        {
            var result = new ValidationResult { IsValid = true };

            if (value is Dictionary<string, object> rules)
            {
                foreach (var (key, rule) in rules)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        result.IsValid = false;
                        result.Errors.Add("Rule key cannot be empty");
                        result.Severity = ValidationSeverity.Error;
                    }

                    if (rule == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Rule '{key}' cannot be null");
                        result.Severity = ValidationSeverity.Error;
                    }
                }
            }

            return Task.FromResult(result);
        }
    }
}