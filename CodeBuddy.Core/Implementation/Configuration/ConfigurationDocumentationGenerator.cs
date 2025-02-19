using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Generates documentation for configuration classes
    /// </summary>
    public class ConfigurationDocumentationGenerator : IDocumentationGenerator
    {
        private readonly Dictionary<string, string> _environmentDescriptions = new()
        {
            ["Development"] = "Local development environment",
            ["Staging"] = "Pre-production testing environment",
            ["Production"] = "Production environment"
        };

        public async Task<string> GenerateDocumentationAsync<T>() where T : BaseConfiguration
        {
            var sb = new StringBuilder();
            var type = typeof(T);

            // Get SchemaVersion attribute
            var schemaAttr = type.GetCustomAttribute<SchemaVersionAttribute>();
            var version = schemaAttr?.Version ?? new Version(1, 0);

            sb.AppendLine($"# {type.Name} Configuration");
            sb.AppendLine();
            
            // Add class description
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                sb.AppendLine(description);
                sb.AppendLine();
            }

            // Add schema version info
            sb.AppendLine($"## Schema Version: {version}");
            sb.AppendLine();

            // Add environment support
            var envAttr = type.GetCustomAttribute<EnvironmentSpecificAttribute>();
            if (envAttr != null)
            {
                sb.AppendLine("## Supported Environments");
                sb.AppendLine();
                foreach (var env in envAttr.ValidEnvironments)
                {
                    sb.AppendLine($"- {env}: {_environmentDescriptions.GetValueOrDefault(env, "Custom environment")}");
                }
                sb.AppendLine();
            }

            // Document properties
            sb.AppendLine("## Configuration Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Required | Description |");
            sb.AppendLine("|----------|------|-----------|-------------|");

            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null);

            foreach (var prop in properties)
            {
                var required = prop.GetCustomAttribute<RequiredAttribute>() != null;
                var propDescription = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var reloadable = prop.GetCustomAttribute<ReloadableAttribute>() != null;
                
                if (reloadable)
                {
                    propDescription += " (Supports hot-reload)";
                }

                sb.AppendLine($"| {prop.Name} | {GetFriendlyTypeName(prop.PropertyType)} | {(required ? "Yes" : "No")} | {propDescription} |");
            }
            sb.AppendLine();

            // Add validation rules
            var validationRules = GetValidationRules(type);
            if (validationRules.Any())
            {
                sb.AppendLine("## Validation Rules");
                sb.AppendLine();
                foreach (var rule in validationRules)
                {
                    sb.AppendLine($"- {rule}");
                }
                sb.AppendLine();
            }

            // Add examples
            sb.AppendLine("## Example Configuration");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(GenerateExampleJson(type));
            sb.AppendLine("```");

            return sb.ToString();
        }

        private string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
                return $"{type.Name.Split('`')[0]}<{genericArgs}>";
            }

            return type.Name;
        }

        private IEnumerable<string> GetValidationRules(Type type)
        {
            var rules = new List<string>();

            foreach (var prop in type.GetProperties())
            {
                var validationAttrs = prop.GetCustomAttributes()
                    .Where(a => a is ValidationAttribute)
                    .Cast<ValidationAttribute>();

                foreach (var attr in validationAttrs)
                {
                    var rule = attr switch
                    {
                        RangeAttribute ra => $"{prop.Name}: Value must be between {ra.Minimum} and {ra.Maximum}",
                        RegularExpressionAttribute rea => $"{prop.Name}: Must match pattern {rea.Pattern}",
                        StringLengthAttribute sla => $"{prop.Name}: Length must be between {sla.MinimumLength} and {sla.MaximumLength} characters",
                        RequiredAttribute => $"{prop.Name}: Value is required",
                        _ => $"{prop.Name}: {attr.GetType().Name.Replace("Attribute", "")} validation"
                    };

                    rules.Add(rule);
                }
            }

            return rules;
        }

        private string GenerateExampleJson(Type type)
        {
            var example = new
            {
                SchemaVersion = "1.0",
                Environment = "Development",
                IsReloadable = true,
                LastModified = DateTime.UtcNow,
                Properties = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null)
                    .ToDictionary(
                        p => p.Name,
                        p => GetExampleValue(p.PropertyType)
                    )
            };

            return System.Text.Json.JsonSerializer.Serialize(example, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private object? GetExampleValue(Type type)
        {
            return type.Name switch
            {
                "String" => "example_value",
                "Int32" => 42,
                "Boolean" => true,
                "Double" => 3.14,
                "DateTime" => DateTime.UtcNow,
                "Version" => new Version(1, 0),
                _ => null
            };
        }
    }
}