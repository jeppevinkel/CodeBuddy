using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Generates documentation for configuration options
/// </summary>
public class ConfigurationDocGenerator
{
    /// <summary>
    /// Generates markdown documentation for the Configuration class
    /// </summary>
    public static string GenerateMarkdown()
    {
        var sb = new StringBuilder();
        var type = typeof(Configuration);

        sb.AppendLine("# CodeBuddy Configuration Documentation");
        sb.AppendLine();
        sb.AppendLine("## Configuration Options");
        sb.AppendLine();

        foreach (var property in type.GetProperties())
        {
            sb.AppendLine($"### {property.Name}");
            sb.AppendLine();

            // Get property type and default value
            var defaultValue = property.GetValue(Activator.CreateInstance<Configuration>());
            sb.AppendLine($"**Type**: `{GetFriendlyTypeName(property.PropertyType)}`");
            sb.AppendLine($"**Default**: `{defaultValue}`");
            sb.AppendLine();

            // Get validation rules
            var validationRules = GetValidationRules(property);
            if (validationRules.Any())
            {
                sb.AppendLine("**Validation Rules**:");
                foreach (var rule in validationRules)
                {
                    sb.AppendLine($"- {rule}");
                }
                sb.AppendLine();
            }

            // Check for encryption
            if (property.GetCustomAttribute<EncryptedAttribute>() != null)
            {
                sb.AppendLine("**Note**: This field is encrypted when stored.");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Environment-Specific Configuration");
        sb.AppendLine();
        sb.AppendLine("Configuration values can be overridden for different environments (Development, Staging, Production) using the `EnvironmentConfigs` section.");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"EnvironmentConfigs\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"Environment\": \"Production\",");
        sb.AppendLine("      \"Overrides\": {");
        sb.AppendLine("        \"MinimumLogLevel\": \"Warning\",");
        sb.AppendLine("        \"EnablePlugins\": true");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{type.Name.Split('`')[0]}<{genericArgs}>";
        }
        return type.Name;
    }

    private static IEnumerable<string> GetValidationRules(PropertyInfo property)
    {
        var rules = new List<string>();

        // Check Required attribute
        if (property.GetCustomAttribute<RequiredAttribute>() != null)
        {
            rules.Add("Required field");
        }

        // Check DirectoryExists attribute
        if (property.GetCustomAttribute<DirectoryExistsAttribute>() != null)
        {
            rules.Add("Directory must exist");
        }

        // Check Range attribute
        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr != null)
        {
            rules.Add($"Value must be between {rangeAttr.Minimum} and {rangeAttr.Maximum}");
        }

        return rules;
    }
}