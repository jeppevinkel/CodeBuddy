using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationValidator : IConfigurationValidator
    {
        public IEnumerable<ValidationResult> Validate<T>(T configuration) where T : class
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(configuration);

            // Perform DataAnnotations validation
            Validator.TryValidateObject(configuration, context, results, validateAllProperties: true);

            // Check schema version attribute
            var schemaVersion = configuration.GetType().GetCustomAttribute<SchemaVersionAttribute>();
            if (schemaVersion == null)
            {
                results.Add(new ValidationResult(
                    "Configuration class must be decorated with SchemaVersionAttribute",
                    new[] { "SchemaVersion" }));
            }

            // Validate nested objects
            foreach (var prop in typeof(T).GetProperties())
            {
                var value = prop.GetValue(configuration);
                if (value != null && IsComplexType(prop.PropertyType))
                {
                    var nestedResults = ValidateNestedObject(value, prop.Name);
                    results.AddRange(nestedResults);
                }
            }

            return results;
        }

        private IEnumerable<ValidationResult> ValidateNestedObject(object value, string propertyName)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(value);

            Validator.TryValidateObject(value, context, results, validateAllProperties: true);

            // Prefix validation errors with property name for clarity
            return results.Select(r => new ValidationResult(
                $"{propertyName}: {r.ErrorMessage}",
                r.MemberNames.Select(m => $"{propertyName}.{m}")
            ));
        }

        private bool IsComplexType(Type type)
        {
            return !type.IsPrimitive && 
                   type != typeof(string) && 
                   type != typeof(decimal) &&
                   type != typeof(DateTime) &&
                   type != typeof(TimeSpan) &&
                   !type.IsEnum;
        }
    }
}