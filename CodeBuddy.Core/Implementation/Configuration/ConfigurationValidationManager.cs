using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration validation with support for custom validation rules
    /// </summary>
    public class ConfigurationValidationManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, List<ValidationAttribute>> _validationRules = new();
        private readonly Dictionary<Type, List<IConfigurationValidator>> _customValidators = new();

        public ConfigurationValidationManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Registers a configuration type for validation
        /// </summary>
        public void RegisterConfiguration<T>() where T : class
        {
            var type = typeof(T);
            if (!_validationRules.ContainsKey(type))
            {
                var rules = new List<ValidationAttribute>();
                var validators = new List<IConfigurationValidator>();

                // Collect validation attributes
                foreach (var prop in type.GetProperties())
                {
                    var attributes = prop.GetCustomAttributes<ValidationAttribute>();
                    rules.AddRange(attributes);
                }

                // Get custom validators from DI
                var customValidators = _serviceProvider.GetServices<IConfigurationValidator>();
                foreach (var validator in customValidators)
                {
                    if (validator.CanValidate(type))
                    {
                        validators.Add(validator);
                    }
                }

                _validationRules[type] = rules;
                _customValidators[type] = validators;
            }
        }

        /// <summary>
        /// Validates a configuration instance
        /// </summary>
        public async Task<(bool IsValid, ValidationSeverity Severity, List<string> Errors)> ValidateAsync<T>(T configuration) where T : class
        {
            var type = typeof(T);
            var errors = new List<string>();
            var maxSeverity = ValidationSeverity.Warning;

            // Register if not already registered
            if (!_validationRules.ContainsKey(type))
            {
                RegisterConfiguration<T>();
            }

            // Validate using attributes
            var validationContext = new ValidationContext(configuration);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
            {
                foreach (var result in validationResults)
                {
                    errors.Add(result.ErrorMessage ?? "Unknown validation error");
                    maxSeverity = ValidationSeverity.Error;
                }
            }

            // Check custom validators
            if (_customValidators.TryGetValue(type, out var validators))
            {
                foreach (var validator in validators)
                {
                    var result = await validator.ValidateAsync(configuration);
                    if (!result.IsValid)
                    {
                        errors.AddRange(result.Errors);
                        maxSeverity = result.Severity > maxSeverity ? result.Severity : maxSeverity;
                    }
                }
            }

            // Validate environment-specific properties
            ValidateEnvironmentSpecific(configuration, errors);

            // Validate sensitive data attributes
            ValidateSensitiveData(configuration, errors);

            return (errors.Count == 0, maxSeverity, errors);
        }

        private void ValidateEnvironmentSpecific<T>(T configuration, List<string> errors) where T : class
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            
            foreach (var prop in typeof(T).GetProperties())
            {
                var envAttr = prop.GetCustomAttribute<EnvironmentSpecificAttribute>();
                if (envAttr != null)
                {
                    var value = prop.GetValue(configuration);
                    if (value != null && !string.IsNullOrEmpty(currentEnv) && 
                        !Array.Exists(envAttr.Environments, e => e.Equals(currentEnv, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"Property {prop.Name} is only valid for environments: {string.Join(", ", envAttr.Environments)}");
                    }
                }
            }
        }

        private void ValidateSensitiveData<T>(T configuration, List<string> errors) where T : class
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
                {
                    var value = prop.GetValue(configuration)?.ToString();
                    if (!string.IsNullOrEmpty(value) && value.StartsWith("$"))
                    {
                        errors.Add($"Sensitive property {prop.Name} appears to contain an unencrypted value");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Severity level for configuration validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }
}