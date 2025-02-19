using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration validation including custom validation rules
    /// </summary>
    public class ConfigurationValidationManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, IConfigurationValidator> _validators = new();

        public ConfigurationValidationManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RegisterConfiguration<T>() where T : class
        {
            var type = typeof(T);
            if (!_validators.ContainsKey(type))
            {
                var validator = _serviceProvider.GetService<IConfigurationValidator<T>>();
                if (validator != null)
                {
                    _validators[type] = validator;
                }
            }
        }

        public async Task<(bool IsValid, ValidationSeverity Severity, IEnumerable<string> Errors)> ValidateAsync<T>(T configuration) where T : class
        {
            var errors = new List<string>();
            var severity = ValidationSeverity.None;

            // Standard validation
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(configuration);
            
            if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
            {
                errors.AddRange(validationResults.Select(r => r.ErrorMessage!));
                severity = ValidationSeverity.Error;
            }

            // Environment-specific validation
            var envSpecificErrors = ValidateEnvironmentSpecific(configuration);
            if (envSpecificErrors.Any())
            {
                errors.AddRange(envSpecificErrors);
                severity = ValidationSeverity.Error;
            }

            // Custom validator
            if (_validators.TryGetValue(typeof(T), out var validator))
            {
                var customValidation = await validator.ValidateAsync(configuration);
                if (!customValidation.IsValid)
                {
                    errors.AddRange(customValidation.Errors);
                    severity = customValidation.Severity;
                }
            }

            return (!errors.Any(), severity, errors);
        }

        private IEnumerable<string> ValidateEnvironmentSpecific<T>(T configuration)
        {
            var errors = new List<string>();
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            foreach (var prop in typeof(T).GetProperties())
            {
                var envAttr = prop.GetCustomAttribute<EnvironmentSpecificAttribute>();
                if (envAttr != null)
                {
                    if (!string.IsNullOrEmpty(currentEnv) && 
                        !envAttr.Environments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"Property {prop.Name} is only valid in environments: {string.Join(", ", envAttr.Environments)}");
                    }
                }
            }

            return errors;
        }
    }

    public enum ValidationSeverity
    {
        None,
        Warning,
        Error
    }
}