using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Generates documentation for configuration sections
    /// </summary>
    public class ConfigurationDocumentationGenerator
    {
        private readonly ILogger<ConfigurationDocumentationGenerator> _logger;
        private readonly Assembly[] _assemblies;

        public ConfigurationDocumentationGenerator(
            ILogger<ConfigurationDocumentationGenerator> logger,
            params Assembly[] assemblies)
        {
            _logger = logger;
            _assemblies = assemblies;
        }

        /// <summary>
        /// Generates markdown documentation for all configuration sections
        /// </summary>
        public async Task<string> GenerateDocumentation()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Configuration Documentation");
                sb.AppendLine();
                sb.AppendLine("This document describes all available configuration options.");
                sb.AppendLine();

                var configTypes = GetConfigurationTypes();
                foreach (var type in configTypes)
                {
                    DocumentConfigurationType(sb, type);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating configuration documentation");
                throw;
            }
        }

        private IEnumerable<Type> GetConfigurationTypes()
        {
            return _assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(BaseConfiguration).IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => t.Name);
        }

        private void DocumentConfigurationType(StringBuilder sb, Type type)
        {
            var sectionAttr = type.GetCustomAttribute<ConfigurationSectionAttribute>();
            sb.AppendLine($"## {type.Name}");
            sb.AppendLine();

            // Add description if available
            var typeDescription = type.GetCustomAttribute<DescriptionAttribute>();
            if (typeDescription != null)
            {
                sb.AppendLine(typeDescription.Description);
                sb.AppendLine();
            }

            // Add version info
            if (sectionAttr != null)
            {
                sb.AppendLine($"**Version:** {sectionAttr.Version}");
                sb.AppendLine();
            }

            // Document properties
            sb.AppendLine("### Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Description | Validation | Default |");
            sb.AppendLine("|----------|------|-------------|------------|---------|");

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType != typeof(BaseConfiguration));

            foreach (var prop in properties)
            {
                DocumentProperty(sb, prop);
            }

            sb.AppendLine();
        }

        private void DocumentProperty(StringBuilder sb, PropertyInfo prop)
        {
            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var validation = GetValidationRules(prop);
            var defaultValue = GetDefaultValue(prop);

            sb.AppendLine($"| {prop.Name} | {GetFriendlyTypeName(prop.PropertyType)} | {description} | {validation} | {defaultValue} |");
        }

        private string GetValidationRules(PropertyInfo prop)
        {
            var rules = new List<string>();

            // Required
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            {
                rules.Add("Required");
            }

            // Range
            var range = prop.GetCustomAttribute<RangeAttribute>();
            if (range != null)
            {
                rules.Add($"Range: {range.Minimum} to {range.Maximum}");
            }

            // String length
            var length = prop.GetCustomAttribute<StringLengthAttribute>();
            if (length != null)
            {
                rules.Add($"Length: {length.MinimumLength} to {length.MaximumLength}");
            }

            // Environment specific
            var envSpecific = prop.GetCustomAttribute<EnvironmentSpecificAttribute>();
            if (envSpecific != null)
            {
                rules.Add($"Environments: {string.Join(", ", envSpecific.Environments)}");
            }

            // Sensitive data
            if (prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
            {
                rules.Add("Sensitive");
            }

            // Reloadable
            var reloadable = prop.GetCustomAttribute<ReloadableAttribute>();
            if (reloadable != null)
            {
                rules.Add($"Reloadable ({reloadable.PollInterval}s)");
            }

            return string.Join("<br>", rules);
        }

        private string GetDefaultValue(PropertyInfo prop)
        {
            var defaultAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultAttr != null)
            {
                return defaultAttr.Value?.ToString() ?? "null";
            }

            if (prop.PropertyType.IsValueType)
            {
                return Activator.CreateInstance(prop.PropertyType)?.ToString() ?? "0";
            }

            return "null";
        }

        private string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(DateTime)) return "datetime";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return $"list<{GetFriendlyTypeName(type.GetGenericArguments()[0])}>";
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"dictionary<{GetFriendlyTypeName(args[0])}, {GetFriendlyTypeName(args[1])}>";
            }
            return type.Name;
        }
    }
}