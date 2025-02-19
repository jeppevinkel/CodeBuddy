using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Generates documentation for configuration types and schemas
    /// </summary>
    public class ConfigurationDocumentationGenerator
    {
        /// <summary>
        /// Generates markdown documentation for a configuration type
        /// </summary>
        public static string GenerateDocumentation(Type configType)
        {
            var doc = new StringBuilder();
            var sectionAttr = configType.GetCustomAttribute<ConfigurationSectionAttribute>();
            
            doc.AppendLine($"# {configType.Name}");
            doc.AppendLine();
            
            if (sectionAttr != null)
            {
                doc.AppendLine($"**Schema Version:** {sectionAttr.Version}");
                doc.AppendLine();
            }

            var description = configType.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                doc.AppendLine(description);
                doc.AppendLine();
            }

            doc.AppendLine("## Properties");
            doc.AppendLine();

            foreach (var prop in configType.GetProperties())
            {
                GeneratePropertyDocumentation(prop, doc);
            }

            return doc.ToString();
        }

        private static void GeneratePropertyDocumentation(PropertyInfo prop, StringBuilder doc)
        {
            doc.AppendLine($"### {prop.Name}");
            
            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                doc.AppendLine();
                doc.AppendLine(description);
            }

            doc.AppendLine();
            doc.AppendLine($"**Type:** {GetFriendlyTypeName(prop.PropertyType)}");

            var validationAttrs = prop.GetCustomAttributes<ValidationAttribute>();
            var hasValidation = false;
            foreach (var attr in validationAttrs)
            {
                if (!hasValidation)
                {
                    doc.AppendLine();
                    doc.AppendLine("**Validation:**");
                    hasValidation = true;
                }

                doc.AppendLine($"- {GetValidationDescription(attr)}");
            }

            var specialAttrs = GetSpecialAttributes(prop);
            if (specialAttrs.Length > 0)
            {
                doc.AppendLine();
                doc.AppendLine("**Special Attributes:**");
                foreach (var attr in specialAttrs)
                {
                    doc.AppendLine($"- {attr}");
                }
            }

            doc.AppendLine();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                var typeName = type.Name.Split('`')[0];
                return $"{typeName}<{string.Join(", ", genericArgs.Select(GetFriendlyTypeName))}>";
            }

            return type.Name;
        }

        private static string GetValidationDescription(ValidationAttribute attr)
        {
            return attr switch
            {
                RequiredAttribute => "Required",
                RangeAttribute range => $"Range: {range.Minimum} to {range.Maximum}",
                StringLengthAttribute length => $"Length: {length.MinimumLength} to {length.MaximumLength}",
                RegularExpressionAttribute regex => $"Pattern: {regex.Pattern}",
                _ => attr.GetType().Name.Replace("Attribute", "")
            };
        }

        private static string[] GetSpecialAttributes(PropertyInfo prop)
        {
            var attrs = new List<string>();

            if (prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
                attrs.Add("Sensitive Data - Stored Securely");

            if (prop.GetCustomAttribute<EnvironmentSpecificAttribute>() is EnvironmentSpecificAttribute env)
                attrs.Add($"Environment Specific: {string.Join(", ", env.Environments)}");

            if (prop.GetCustomAttribute<ReloadableAttribute>() is ReloadableAttribute reload)
                attrs.Add($"Reloadable (Poll Interval: {reload.PollInterval}s)");

            if (prop.GetCustomAttribute<CommandLineOverrideAttribute>() is CommandLineOverrideAttribute cmd)
                attrs.Add($"Command Line Override: --{cmd.ArgumentName}" + (cmd.Required ? " (Required)" : ""));

            return attrs.ToArray();
        }
    }
}