using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Marks a property as environment-specific with allowed environments
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentSpecificAttribute : ValidationAttribute
    {
        public string[] Environments { get; }

        public EnvironmentSpecificAttribute(params string[] environments)
        {
            Environments = environments;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.IsNullOrEmpty(currentEnv)) return ValidationResult.Success;

            if (!Environments.Contains(currentEnv, StringComparer.OrdinalIgnoreCase))
            {
                return new ValidationResult(
                    $"Property {validationContext.MemberName} is not valid for environment {currentEnv}. " +
                    $"Allowed environments: {string.Join(", ", Environments)}");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Marks a property as reloadable at runtime
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReloadableAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a property as containing sensitive data that should be encrypted
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveDataAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            var stringValue = value.ToString();
            if (string.IsNullOrEmpty(stringValue)) return ValidationResult.Success;

            // Check if value appears to be unencrypted
            if (stringValue.StartsWith("$"))
            {
                return new ValidationResult(
                    $"Property {validationContext.MemberName} contains sensitive data that must be encrypted");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Marks a property as requiring a backup before modifications
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RequiresBackupAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies valid values for an enum property
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidEnumValuesAttribute : ValidationAttribute
    {
        private readonly Type _enumType;

        public ValidEnumValuesAttribute(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Type must be an enum", nameof(enumType));
            }
            _enumType = enumType;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            if (!Enum.IsDefined(_enumType, value))
            {
                return new ValidationResult(
                    $"Value {value} is not valid for enum {_enumType.Name}. " +
                    $"Valid values: {string.Join(", ", Enum.GetNames(_enumType))}");
            }

            return ValidationResult.Success;
        }
    }
}