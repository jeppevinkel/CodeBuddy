using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Validates configuration objects using validation attributes and custom rules
    /// </summary>
    public class ConfigurationValidator : IConfigurationValidator
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Type, List<Func<object, ValidationResult?>>> _customValidators;

        public ConfigurationValidator(ILogger logger)
        {
            _logger = logger;
            _customValidators = new Dictionary<Type, List<Func<object, ValidationResult?>>>();
        }

        /// <summary>
        /// Registers a custom validation rule for a specific configuration type
        /// </summary>
        public void RegisterValidator<T>(Func<T, ValidationResult?> validator)
        {
            var type = typeof(T);
            if (!_customValidators.ContainsKey(type))
            {
                _customValidators[type] = new List<Func<object, ValidationResult?>>();
            }

            _customValidators[type].Add(obj => validator((T)obj));
        }

        /// <summary>
        /// Validates a configuration object and returns all validation errors
        /// </summary>
        public IEnumerable<ValidationResult> Validate<T>(T configuration) where T : class
        {
            var results = new List<ValidationResult>();

            // Skip validation for null configurations
            if (configuration == null)
            {
                results.Add(new ValidationResult("Configuration object cannot be null"));
                return results;
            }

            try
            {
                // Validate using DataAnnotations
                var context = new ValidationContext(configuration);
                var validationResults = new List<ValidationResult>();
                
                if (!Validator.TryValidateObject(configuration, context, validationResults, validateAllProperties: true))
                {
                    results.AddRange(validationResults);
                }

                // Validate nested objects
                ValidateNestedObjects(configuration, results);

                // Run type-specific custom validators
                var configType = typeof(T);
                if (_customValidators.ContainsKey(configType))
                {
                    foreach (var validator in _customValidators[configType])
                    {
                        var result = validator(configuration);
                        if (result != null)
                        {
                            results.Add(result);
                        }
                    }
                }

                // Additional checks for SensitiveData attributes
                ValidateSensitiveData(configuration, results);

                // Validate environment-specific settings
                ValidateEnvironmentSpecific(configuration, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration validation");
                results.Add(new ValidationResult($"Validation error: {ex.Message}"));
            }

            return results;
        }

        private void ValidateNestedObjects(object obj, List<ValidationResult> results)
        {
            var properties = obj.GetType().GetProperties()
                .Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string));

            foreach (var property in properties)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    var nestedContext = new ValidationContext(value)
                    {
                        MemberName = property.Name
                    };

                    var nestedResults = new List<ValidationResult>();
                    Validator.TryValidateObject(value, nestedContext, nestedResults, true);
                    results.AddRange(nestedResults);

                    // Recurse into nested object
                    ValidateNestedObjects(value, results);
                }
            }
        }

        private void ValidateSensitiveData(object obj, List<ValidationResult> results)
        {
            var sensitiveProperties = obj.GetType().GetProperties()
                .Where(p => p.GetCustomAttribute<SensitiveDataAttribute>() != null);

            foreach (var property in sensitiveProperties)
            {
                var value = property.GetValue(obj)?.ToString();
                if (!string.IsNullOrEmpty(value) && !value.StartsWith("enc:"))
                {
                    results.Add(new ValidationResult(
                        $"Property {property.Name} contains unencrypted sensitive data",
                        new[] { property.Name }));
                }
            }
        }

        private void ValidateEnvironmentSpecific(object obj, List<ValidationResult> results)
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            var envProperties = obj.GetType().GetProperties()
                .Where(p => p.GetCustomAttribute<EnvironmentSpecificAttribute>() != null);

            foreach (var property in envProperties)
            {
                var attr = property.GetCustomAttribute<EnvironmentSpecificAttribute>();
                if (attr?.Environments != null && !attr.Environments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase))
                {
                    var value = property.GetValue(obj);
                    if (value != null && !Equals(value, GetDefaultValue(property.PropertyType)))
                    {
                        results.Add(new ValidationResult(
                            $"Property {property.Name} should not be set in {currentEnv} environment",
                            new[] { property.Name }));
                    }
                }
            }
        }

        private static object? GetDefaultValue(Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
    }
}