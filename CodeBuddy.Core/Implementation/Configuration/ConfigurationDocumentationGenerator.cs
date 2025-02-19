using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationDocumentationGenerator
    {
        private readonly string _configBasePath;

        public ConfigurationDocumentationGenerator(string configBasePath)
        {
            _configBasePath = configBasePath;
        }

        public async Task<string> GenerateDocumentation(string configBasePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Configuration Reference");
            sb.AppendLine();
            sb.AppendLine("This document describes all available configuration options for CodeBuddy.");
            sb.AppendLine();

            // Get all configuration classes from the assembly
            var configTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Namespace?.Contains("Configuration") == true && 
                           t.GetCustomAttribute<SchemaVersionAttribute>() != null)
                .OrderBy(t => t.Name);

            foreach (var type in configTypes)
            {
                DocumentConfigurationType(type, sb);
            }

            return sb.ToString();
        }

        private void DocumentConfigurationType(Type type, StringBuilder sb)
        {
            var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>();
            
            sb.AppendLine($"## {FormatTypeName(type.Name)}");
            sb.AppendLine();
            
            // Add version info
            sb.AppendLine($"**Schema Version:** {schemaVersion?.Version ?? "1.0"}");
            sb.AppendLine();

            // Document file location
            var sectionName = type.Name.Replace("Configuration", "").ToLowerInvariant();
            sb.AppendLine($"**File:** `{_configBasePath}/{sectionName}.json`");
            sb.AppendLine();

            // Document properties
            sb.AppendLine("### Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Required | Default | Description |");
            sb.AppendLine("|----------|------|----------|---------|-------------|");

            foreach (var prop in type.GetProperties())
            {
                DocumentProperty(prop, sb);
            }

            sb.AppendLine();

            // Add example
            sb.AppendLine("### Example");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(GenerateExample(type));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        private void DocumentProperty(PropertyInfo prop, StringBuilder sb)
        {
            var required = prop.GetCustomAttribute<RequiredAttribute>() != null;
            var envVar = prop.GetCustomAttribute<EnvironmentVariableAttribute>();
            var secure = prop.GetCustomAttribute<SecureStorageAttribute>() != null;
            var range = prop.GetCustomAttribute<RangeAttribute>();
            
            var description = new StringBuilder();
            
            if (secure)
            {
                description.Append("*(Secure)* ");
            }
            
            if (envVar != null)
            {
                description.Append($"Environment Variable: `{envVar.VariableName}`. ");
            }
            
            if (range != null)
            {
                description.Append($"Range: {range.Minimum} to {range.Maximum}. ");
            }

            sb.AppendLine($"| {prop.Name} | {FormatTypeName(prop.PropertyType.Name)} | {(required ? "Yes" : "No")} | {GetDefaultValue(prop)} | {description} |");
        }

        private string FormatTypeName(string typeName)
        {
            return typeName
                .Replace("Configuration", "")
                .Replace("String", "string")
                .Replace("Int32", "int")
                .Replace("Boolean", "bool");
        }

        private string GetDefaultValue(PropertyInfo prop)
        {
            var defaultValue = prop.PropertyType.IsValueType ? 
                Activator.CreateInstance(prop.PropertyType) : 
                null;
                
            return defaultValue?.ToString() ?? "-";
        }

        private string GenerateExample(Type type)
        {
            var example = new StringBuilder();
            example.AppendLine("{");
            
            var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>();
            example.AppendLine($"  \"version\": \"{schemaVersion?.Version ?? "1.0"}\",");
            example.AppendLine("  \"settings\": {");

            var props = type.GetProperties();
            for (var i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var value = GetExampleValue(prop);
                example.AppendLine($"    \"{prop.Name}\": {value}{(i < props.Length - 1 ? "," : "")}");
            }

            example.AppendLine("  }");
            example.AppendLine("}");

            return example.ToString();
        }

        private string GetExampleValue(PropertyInfo prop)
        {
            if (prop.PropertyType == typeof(string))
            {
                return "\"example\"";
            }
            if (prop.PropertyType == typeof(bool))
            {
                return "true";
            }
            if (prop.PropertyType == typeof(int))
            {
                return "42";
            }
            if (prop.PropertyType.IsEnum)
            {
                var enumValue = Enum.GetValues(prop.PropertyType).GetValue(0);
                return $"\"{enumValue}\"";
            }
            return "null";
        }
    }
}