using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Collections.Generic;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Configuration
{
    public class ConfigurationDocumentationGenerator
    {
        private readonly string _configBasePath;

        public ConfigurationDocumentationGenerator(string configBasePath)
        {
            _configBasePath = configBasePath;
        }

        public async Task<DocumentationResult> GenerateDocumentation(string configBasePath)
        {
            var sb = new StringBuilder();
            
            // Generate primary documentation
            await GenerateMainDocumentation(sb);
            
            // Generate section for configuration sources
            GenerateConfigurationSources(sb);
            
            // Generate environment-specific templates
            await GenerateEnvironmentTemplates(sb);
            
            // Generate security best practices
            GenerateSecurityGuidelines(sb);
            
            // Generate migration documentation
            await GenerateMigrationDocs(sb);
            
            return new DocumentationResult 
            {
                Content = sb.ToString(),
                Generated = DateTime.UtcNow,
                Status = DocumentationStatus.Success
            };
        }

        private async Task GenerateMainDocumentation(StringBuilder sb)
        {
            sb.AppendLine("# Configuration Management System");
            sb.AppendLine();
            sb.AppendLine("The CodeBuddy Configuration Management System provides a robust and flexible way to manage application settings across different environments, with support for validation, versioning, and secure storage.");
            sb.AppendLine();
            sb.AppendLine("## Configuration File Format");
            sb.AppendLine();
            sb.AppendLine("Configuration files are stored as JSON in the `config` directory. Each configuration section has its own file named `{section}.json`.");
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
            var sectionName = type.Name.Replace("Configuration", "");
            
            sb.AppendLine($"## {sectionName} Configuration");
            sb.AppendLine();
            
            // Add metadata
            sb.AppendLine($"**Schema Version:** {schemaVersion?.Version ?? "1.0"}");
            sb.AppendLine($"**File:** `{_configBasePath}/{sectionName.ToLowerInvariant()}.json`");
            if (type.GetCustomAttribute<ObsoleteAttribute>() != null)
            {
                sb.AppendLine("**Status:** Deprecated");
            }
            sb.AppendLine();

            // Document class description
            var description = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (description != null)
            {
                sb.AppendLine(description.Description);
                sb.AppendLine();
            }

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
            var regex = prop.GetCustomAttribute<RegularExpressionAttribute>();
            var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            
            var details = new StringBuilder();
            
            if (secure)
            {
                details.Append("🔒 **Secure Value** - Stored in secure storage. ");
            }
            
            if (envVar != null)
            {
                details.Append($"⚙️ **Environment Variable:** `{envVar.VariableName}` ");
            }
            
            if (range != null)
            {
                details.Append($"📏 **Valid Range:** {range.Minimum} to {range.Maximum} ");
            }

            if (regex != null)
            {
                details.Append($"🔍 **Pattern:** `{regex.Pattern}` ");
            }

            if (description != null)
            {
                details.Append($"\n\n{description.Description}");
            }

            sb.AppendLine($"| {prop.Name} | {FormatTypeName(prop.PropertyType.Name)} | {(required ? "✓" : "")} | {GetDefaultValue(prop)} | {details} |");
        }

        private void GenerateConfigurationSources(StringBuilder sb)
        {
            sb.AppendLine("## Configuration Sources");
            sb.AppendLine();
            sb.AppendLine("The system supports multiple configuration sources in order of precedence:");
            sb.AppendLine();
            sb.AppendLine("1. Command Line Arguments (highest priority)");
            sb.AppendLine("2. Environment Variables");
            sb.AppendLine("3. Configuration Files");
            sb.AppendLine("4. Default Values (lowest priority)");
            sb.AppendLine();
        }

        private async Task GenerateEnvironmentTemplates(StringBuilder sb)
        {
            sb.AppendLine("## Environment Templates");
            sb.AppendLine();
            sb.AppendLine("### Development Environment");
            sb.AppendLine("```json");
            sb.AppendLine(await GenerateTemplateForEnvironment("development"));
            sb.AppendLine("```");
            
            sb.AppendLine("### Production Environment");
            sb.AppendLine("```json");
            sb.AppendLine(await GenerateTemplateForEnvironment("production"));
            sb.AppendLine("```");
        }

        private void GenerateSecurityGuidelines(StringBuilder sb)
        {
            sb.AppendLine("## Security Best Practices");
            sb.AppendLine();
            sb.AppendLine("1. **Sensitive Data Protection**");
            sb.AppendLine("   - Never commit sensitive values to source control");
            sb.AppendLine("   - Use secure storage for sensitive configuration");
            sb.AppendLine("   - Implement proper access controls");
            sb.AppendLine();
            sb.AppendLine("2. **Environment Variables**");
            sb.AppendLine("   - Use environment variables for sensitive values");
            sb.AppendLine("   - Follow the naming convention: `CONFIG_{SECTION}_{KEY}`");
            sb.AppendLine();
            sb.AppendLine("3. **Configuration Validation**");
            sb.AppendLine("   - Validate all configuration values at startup");
            sb.AppendLine("   - Implement thorough validation rules");
            sb.AppendLine("   - Log validation failures appropriately");
            sb.AppendLine();
        }

        private async Task GenerateMigrationDocs(StringBuilder sb)
        {
            sb.AppendLine("## Configuration Migrations");
            sb.AppendLine();
            
            var migrations = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IConfigurationMigration).IsAssignableFrom(t) && !t.IsInterface)
                .OrderBy(t => t.Name);

            foreach (var migration in migrations)
            {
                var instance = Activator.CreateInstance(migration) as IConfigurationMigration;
                if (instance != null)
                {
                    sb.AppendLine($"### Migration {instance.FromVersion} → {instance.ToVersion}");
                    sb.AppendLine();
                    var description = migration.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                    if (description != null)
                    {
                        sb.AppendLine(description.Description);
                        sb.AppendLine();
                    }
                }
            }
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

        private string GetExampleValue(PropertyInfo prop, string environment = null)
        {
            // Get environment-specific example if available
            var envExample = prop.GetCustomAttribute<EnvironmentExampleAttribute>();
            if (environment != null && envExample != null)
            {
                var value = envExample.GetValueForEnvironment(environment);
                if (value != null)
                {
                    return JsonSerialize(value);
                }
            }

            // Get default example value
            if (prop.PropertyType == typeof(string))
            {
                if (prop.GetCustomAttribute<SecureStorageAttribute>() != null)
                {
                    return "\"[secure value]\"";
                }
                return "\"example\"";
            }
            if (prop.PropertyType == typeof(bool))
            {
                return environment == "production" ? "false" : "true";
            }
            if (prop.PropertyType == typeof(int))
            {
                if (prop.Name.Contains("Port"))
                {
                    return environment == "production" ? "443" : "8080";
                }
                if (prop.Name.Contains("Timeout"))
                {
                    return environment == "production" ? "30" : "120";
                }
                return "42";
            }
            if (prop.PropertyType.IsEnum)
            {
                var enumValue = Enum.GetValues(prop.PropertyType).GetValue(0);
                return $"\"{enumValue}\"";
            }
            return "null";
        }

        private string JsonSerialize(object value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{str}\"";
            if (value is bool) return value.ToString().ToLower();
            if (value is int or long or float or double) return value.ToString();
            return $"\"{value}\"";
        }

        private async Task<string> GenerateTemplateForEnvironment(string environment)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            
            // Get all configuration types
            var configTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Namespace?.Contains("Configuration") == true && 
                           t.GetCustomAttribute<SchemaVersionAttribute>() != null)
                .OrderBy(t => t.Name);

            var isFirst = true;
            foreach (var type in configTypes)
            {
                if (!isFirst) sb.AppendLine(",");
                isFirst = false;

                var sectionName = type.Name.Replace("Configuration", "").ToLowerInvariant();
                sb.AppendLine($"  \"{sectionName}\": {{");
                sb.AppendLine($"    \"version\": \"{type.GetCustomAttribute<SchemaVersionAttribute>()?.Version ?? "1.0"}\",");
                sb.AppendLine("    \"settings\": {");

                var props = type.GetProperties();
                for (var i = 0; i < props.Length; i++)
                {
                    var prop = props[i];
                    var value = GetExampleValue(prop, environment);
                    sb.AppendLine($"      \"{prop.Name}\": {value}{(i < props.Length - 1 ? "," : "")}");
                }

                sb.AppendLine("    }");
                sb.AppendLine("  }");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}