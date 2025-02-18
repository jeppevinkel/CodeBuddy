using System;

namespace CodeBuddy.Core.Models.Configuration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationSectionAttribute : Attribute
    {
        public string SectionName { get; }
        public string Description { get; }
        public int Version { get; }

        public ConfigurationSectionAttribute(string sectionName, string description, int version = 1)
        {
            SectionName = sectionName;
            Description = description;
            Version = version;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurationItemAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; }
        public string DefaultValue { get; }

        public ConfigurationItemAttribute(string description, bool required = true, string defaultValue = null)
        {
            Description = description;
            Required = required;
            DefaultValue = defaultValue;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RangeValidationAttribute : Attribute
    {
        public object Minimum { get; }
        public object Maximum { get; }

        public RangeValidationAttribute(object minimum, object maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PatternValidationAttribute : Attribute
    {
        public string RegexPattern { get; }
        public string ErrorMessage { get; }

        public PatternValidationAttribute(string regexPattern, string errorMessage)
        {
            RegexPattern = regexPattern;
            ErrorMessage = errorMessage;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CustomValidationAttribute : Attribute
    {
        public Type ValidatorType { get; }

        public CustomValidationAttribute(Type validatorType)
        {
            if (!typeof(IConfigurationValidator).IsAssignableFrom(validatorType))
            {
                throw new ArgumentException("Validator type must implement IConfigurationValidator");
            }
            ValidatorType = validatorType;
        }
    }
}