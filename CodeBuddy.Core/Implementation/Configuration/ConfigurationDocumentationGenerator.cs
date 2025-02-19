using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Generates documentation for configuration classes and their settings
    /// </summary>
    public class ConfigurationDocumentationGenerator
    {
        private readonly IConfigurationManager _configManager;

        public ConfigurationDocumentationGenerator(IConfigurationManager configManager)
        {
            _configManager = configManager;
        }

        /// <summary>
        /// Generates markdown documentation for all configuration sections
        /// </summary>
        public async Task<string> GenerateDocumentation(string configBasePath)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Configuration Documentation");
            sb.AppendLine();
            sb.AppendLine("This document describes all available configuration settings and their usage.");
            sb.AppendLine();

            // Get all configuration types from the assembly
            var configTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(BaseConfiguration).IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var configType in configTypes)
            {
                await DocumentConfigurationType(configType, sb);
            }

            sb.AppendLine("## Environment Variables");
            sb.AppendLine();
            sb.AppendLine("Configuration values can be overridden using environment variables following this pattern:");
            sb.AppendLine("`CONFIG_[SectionName]_[PropertyName]`");
            sb.AppendLine();
            sb.AppendLine("Example: `CONFIG_LOGGING_LOGLEVEL=Debug`");
            sb.AppendLine();

            sb.AppendLine("## Secure Configuration");
            sb.AppendLine();
            sb.AppendLine("Properties marked with `[SensitiveData]` are automatically encrypted when stored.");
            sb.AppendLine("These values should be set using the secure configuration commands:");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine("codebuddy config set-secure [section] [key] [value]");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private async Task DocumentConfigurationType(Type configType, StringBuilder sb)
        {
            var sectionName = configType.Name.Replace("Configuration", "");
            
            sb.AppendLine($"## {sectionName} Configuration");
            sb.AppendLine();

            // Add type description
            var typeDescription = configType.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(typeDescription))
            {
                sb.AppendLine(typeDescription);
                sb.AppendLine();
            }

            // Get current configuration if it exists
            var metadata = await _configManager.GetConfigurationMetadata(sectionName);
            if (metadata != null)
            {
                sb.AppendLine("### Metadata");
                sb.AppendLine();
                sb.AppendLine("| Property | Value |");
                sb.AppendLine("|----------|--------|");
                foreach (var kvp in metadata)
                {
                    sb.AppendLine($"| {kvp.Key} | {kvp.Value} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("### Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Default | Description | Validation |");
            sb.AppendLine("|----------|------|---------|-------------|------------|");

            var properties = configType.GetProperties()
                .Where(p => p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
                .OrderBy(p => p.Name);

            foreach (var property in properties)
            {
                var defaultValue = GetDefaultValue(property);
                var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var validation = GetValidationRules(property);

                sb.AppendLine($"| {property.Name} | {GetTypeDescription(property.PropertyType)} | {defaultValue} | {description} | {validation} |");
            }
            sb.AppendLine();

            // Document any nested configuration classes
            var nestedTypes = configType.GetProperties()
                .Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string))
                .Select(p => p.PropertyType)
                .Distinct();

            foreach (var nestedType in nestedTypes)
            {
                sb.AppendLine($"### {nestedType.Name}");
                sb.AppendLine();
                await DocumentConfigurationType(nestedType, sb);
            }
        }

        private string GetTypeDescription(Type type)
        {
            if (type.IsEnum)
            {
                var values = string.Join(", ", Enum.GetNames(type));
                return $"Enum ({values})";
            }

            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(TimeSpan)) return "timespan";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return $"list of {GetTypeDescription(type.GetGenericArguments()[0])}";
            }

            return type.Name;
        }

        private string GetDefaultValue(PropertyInfo property)
        {
            var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultAttr != null)
            {
                return defaultAttr.Value?.ToString() ?? "null";
            }

            if (property.PropertyType.IsValueType)
            {
                return Activator.CreateInstance(property.PropertyType)?.ToString() ?? "0";
            }

            return "null";
        }

        private string GetValidationRules(PropertyInfo property)
        {
            var rules = new List<string>();

            // Required validation
            if (property.GetCustomAttribute<RequiredAttribute>() != null)
            {
                rules.Add("Required");
            }

            // Range validation
            var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                rules.Add($"Range: {rangeAttr.Minimum} to {rangeAttr.Maximum}");
            }

            // String length
            var stringLengthAttr = property.GetCustomAttribute<StringLengthAttribute>();
            if (stringLengthAttr != null)
            {
                rules.Add($"Length: {stringLengthAttr.MinimumLength} to {stringLengthAttr.MaximumLength}");
            }

            // RegularExpression
            var regexAttr = property.GetCustomAttribute<RegularExpressionAttribute>();
            if (regexAttr != null)
            {
                rules.Add($"Pattern: {regexAttr.Pattern}");
            }

            // Environment specific
            var envAttr = property.GetCustomAttribute<EnvironmentSpecificAttribute>();
            if (envAttr != null)
            {
                rules.Add($"Environments: {string.Join(", ", envAttr.Environments)}");
            }

            // Sensitive data
            if (property.GetCustomAttribute<SensitiveDataAttribute>() != null)
            {
                rules.Add("Sensitive");
            }

            // Reloadable
            if (property.GetCustomAttribute<ReloadableAttribute>() != null)
            {
                rules.Add("Reloadable");
            }

            return string.Join("; ", rules);
        }
    }
}