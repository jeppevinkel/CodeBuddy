using System;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Marks a configuration property as sensitive, indicating it should be stored securely
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveDataAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            return true; // Just marks for secure storage, doesn't validate
        }
    }

    /// <summary>
    /// Indicates a property requires specific environment variables to be set
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RequiresEnvironmentAttribute : ValidationAttribute
    {
        private readonly string[] _requiredVars;

        public RequiresEnvironmentAttribute(params string[] envVars)
        {
            _requiredVars = envVars;
        }

        public override bool IsValid(object value)
        {
            foreach (var env in _requiredVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(env)))
                {
                    ErrorMessage = $"Environment variable {env} is required but not set";
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Validates a configuration value against command line arguments
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CommandLineOverrideAttribute : ValidationAttribute
    {
        public string ArgumentName { get; }
        public bool Required { get; }

        public CommandLineOverrideAttribute(string argumentName, bool required = false)
        {
            ArgumentName = argumentName;
            Required = required;
        }
    }

    /// <summary>
    /// Indicates configuration values that should be reloaded when changed
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReloadableAttribute : Attribute
    {
        public int PollInterval { get; }

        public ReloadableAttribute(int pollIntervalSeconds = 30)
        {
            PollInterval = pollIntervalSeconds;
        }
    }

    /// <summary>
    /// Specifies valid configuration value ranges
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurationRangeAttribute : RangeAttribute
    {
        public ConfigurationRangeAttribute(double minimum, double maximum) 
            : base(minimum, maximum)
        {
        }

        public ConfigurationRangeAttribute(int minimum, int maximum) 
            : base(minimum, maximum)
        {
        }

        public ConfigurationRangeAttribute(Type type, string minimum, string maximum) 
            : base(type, minimum, maximum)
        {
        }
    }

    /// <summary>
    /// Marks configuration values that are environment-specific
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentSpecificAttribute : Attribute
    {
        public string[] Environments { get; }

        public EnvironmentSpecificAttribute(params string[] environments)
        {
            Environments = environments;
        }
    }
}