using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration validation and schema versioning
    /// </summary>
    public class ConfigurationValidationManager : IConfigurationValidator
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Type, IList<ValidationAttribute>> _customValidators = new();
        private readonly HashSet<Type> _reloadableTypes = new();

        public ConfigurationValidationManager(ILogger logger)
        {
            _logger = logger;
        }

        public void RegisterValidationRules<T>(IEnumerable<ValidationAttribute> validators)
        {
            if (!_customValidators.ContainsKey(typeof(T)))
            {
                _customValidators[typeof(T)] = new List<ValidationAttribute>();
            }

            foreach (var validator in validators)
            {
                _customValidators[typeof(T)].Add(validator);
            }
        }

        public void RegisterReloadableConfiguration<T>()
        {
            _reloadableTypes.Add(typeof(T));
        }

        public async Task<ValidationResult?> ValidateConfigurationAsync<T>(T configuration) where T : BaseConfiguration
        {
            if (configuration == null)
            {
                return new ValidationResult("Configuration cannot be null");
            }

            try
            {
                // Run base configuration validation
                var baseResult = configuration.Validate();
                if (baseResult != ValidationResult.Success)
                {
                    return baseResult;
                }

                // Validate environment
                if (!string.IsNullOrEmpty(configuration.Environment))
                {
                    var envAttr = typeof(T).GetCustomAttributes(typeof(EnvironmentSpecificAttribute), true)
                        .FirstOrDefault() as EnvironmentSpecificAttribute;

                    if (envAttr != null && !envAttr.ValidEnvironments.Contains(configuration.Environment))
                    {
                        return new ValidationResult(
                            $"Invalid environment '{configuration.Environment}'. Valid values are: {string.Join(", ", envAttr.ValidEnvironments)}");
                    }
                }

                // Check if hot-reload is allowed
                if (configuration.IsReloadable && !_reloadableTypes.Contains(typeof(T)))
                {
                    return new ValidationResult($"Configuration type {typeof(T).Name} does not support hot-reload");
                }

                // Run custom validators
                if (_customValidators.TryGetValue(typeof(T), out var validators))
                {
                    foreach (var validator in validators)
                    {
                        var context = new ValidationContext(configuration);
                        var results = new List<ValidationResult>();

                        if (!Validator.TryValidateValue(configuration, context, results, new[] { validator }))
                        {
                            return results.FirstOrDefault();
                        }
                    }
                }

                // Run data annotation validations
                var validationResults = new List<ValidationResult>();
                var validationContext = new ValidationContext(configuration);

                if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
                {
                    return validationResults.FirstOrDefault();
                }

                _logger.LogInformation("Configuration validation successful for type {Type}", typeof(T).Name);
                return ValidationResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration of type {Type}", typeof(T).Name);
                return new ValidationResult($"Validation error: {ex.Message}");
            }
        }

        public bool IsReloadable<T>()
        {
            return _reloadableTypes.Contains(typeof(T));
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ReloadableAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class EnvironmentSpecificAttribute : Attribute
    {
        public string[] ValidEnvironments { get; }

        public EnvironmentSpecificAttribute(params string[] validEnvironments)
        {
            ValidEnvironments = validEnvironments;
        }
    }
}