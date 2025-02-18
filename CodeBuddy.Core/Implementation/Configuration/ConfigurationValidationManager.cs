using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationValidationManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, ConfigurationMetadata> _configMetadata;

        public ConfigurationValidationManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configMetadata = new Dictionary<Type, ConfigurationMetadata>();
        }

        public void RegisterConfiguration<T>() where T : class
        {
            var type = typeof(T);
            if (_configMetadata.ContainsKey(type))
                return;

            var metadata = new ConfigurationMetadata(type);
            _configMetadata.Add(type, metadata);
        }

        public async Task<ValidationResult> ValidateAsync<T>(T configuration) where T : class
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var type = typeof(T);
            if (!_configMetadata.TryGetValue(type, out var metadata))
                throw new InvalidOperationException($"Configuration type {type.Name} is not registered");

            var result = new ValidationResult { IsValid = true };
            var context = new ValidationContext();

            foreach (var property in metadata.Properties)
            {
                var propertyValue = property.GetValue(configuration);
                context.PropertyName = property.Name;
                context.Instance = configuration;

                foreach (var validator in property.Validators)
                {
                    var validationResult = await validator.ValidateAsync(propertyValue, context);
                    if (!validationResult.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.AddRange(validationResult.Errors);
                        
                        if (validationResult.Severity == ValidationSeverity.Error)
                            break;
                    }
                }
            }

            return result;
        }

        private class ConfigurationMetadata
        {
            public Type Type { get; }
            public ConfigurationSectionAttribute SectionAttribute { get; }
            public List<PropertyMetadata> Properties { get; }

            public ConfigurationMetadata(Type type)
            {
                Type = type;
                SectionAttribute = type.GetCustomAttribute<ConfigurationSectionAttribute>();
                Properties = type.GetProperties()
                    .Select(p => new PropertyMetadata(p))
                    .ToList();
            }
        }

        private class PropertyMetadata
        {
            public PropertyInfo Property { get; }
            public List<IConfigurationValidator> Validators { get; }

            public PropertyMetadata(PropertyInfo property)
            {
                Property = property;
                Validators = BuildValidators(property);
            }

            private List<IConfigurationValidator> BuildValidators(PropertyInfo property)
            {
                var validators = new List<IConfigurationValidator>();

                var configItem = property.GetCustomAttribute<ConfigurationItemAttribute>();
                if (configItem != null)
                {
                    validators.Add(new RequiredValidator(configItem.Required));
                }

                var rangeAttr = property.GetCustomAttribute<RangeValidationAttribute>();
                if (rangeAttr != null)
                {
                    validators.Add(new RangeValidator(rangeAttr.Minimum, rangeAttr.Maximum));
                }

                var patternAttr = property.GetCustomAttribute<PatternValidationAttribute>();
                if (patternAttr != null)
                {
                    validators.Add(new PatternValidator(patternAttr.RegexPattern, patternAttr.ErrorMessage));
                }

                var customAttr = property.GetCustomAttribute<CustomValidationAttribute>();
                if (customAttr != null)
                {
                    var validator = (IConfigurationValidator)Activator.CreateInstance(customAttr.ValidatorType);
                    validators.Add(validator);
                }

                return validators;
            }
        }
    }

    internal class RequiredValidator : IConfigurationValidator
    {
        private readonly bool _required;

        public RequiredValidator(bool required)
        {
            _required = required;
        }

        public Task<ValidationResult> ValidateAsync(object value, ValidationContext context)
        {
            var result = new ValidationResult { IsValid = true };

            if (_required && value == null)
            {
                result.IsValid = false;
                result.Errors.Add($"Property {context.PropertyName} is required");
                result.Severity = ValidationSeverity.Error;
            }

            return Task.FromResult(result);
        }
    }

    internal class RangeValidator : IConfigurationValidator
    {
        private readonly object _min;
        private readonly object _max;

        public RangeValidator(object min, object max)
        {
            _min = min;
            _max = max;
        }

        public Task<ValidationResult> ValidateAsync(object value, ValidationContext context)
        {
            var result = new ValidationResult { IsValid = true };

            if (value == null)
                return Task.FromResult(result);

            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(_min) < 0 || comparable.CompareTo(_max) > 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Property {context.PropertyName} must be between {_min} and {_max}");
                    result.Severity = ValidationSeverity.Error;
                }
            }

            return Task.FromResult(result);
        }
    }

    internal class PatternValidator : IConfigurationValidator
    {
        private readonly string _pattern;
        private readonly string _errorMessage;

        public PatternValidator(string pattern, string errorMessage)
        {
            _pattern = pattern;
            _errorMessage = errorMessage;
        }

        public Task<ValidationResult> ValidateAsync(object value, ValidationContext context)
        {
            var result = new ValidationResult { IsValid = true };

            if (value == null)
                return Task.FromResult(result);

            if (value is string strValue)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(strValue, _pattern))
                {
                    result.IsValid = false;
                    result.Errors.Add(_errorMessage ?? $"Property {context.PropertyName} does not match the required pattern");
                    result.Severity = ValidationSeverity.Error;
                }
            }

            return Task.FromResult(result);
        }
    }
}