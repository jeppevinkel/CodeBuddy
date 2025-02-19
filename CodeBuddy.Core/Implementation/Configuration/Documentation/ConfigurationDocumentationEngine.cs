using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration.Documentation
{
    /// <summary>
    /// Generates comprehensive documentation for configuration types
    /// </summary>
    public class ConfigurationDocumentationEngine
    {
        private readonly MigrationEngine _migrationEngine;
        private readonly Dictionary<string, string> _environmentDescriptions;
        private readonly string _outputPath;

        public ConfigurationDocumentationEngine(
            MigrationEngine migrationEngine,
            string outputPath)
        {
            _migrationEngine = migrationEngine;
            _outputPath = outputPath;
            _environmentDescriptions = new Dictionary<string, string>
            {
                ["Development"] = "Local development environment for debugging and testing",
                ["Staging"] = "Pre-production environment for final testing and validation",
                ["Production"] = "Live production environment with strict security controls"
            };
        }

        public async Task GenerateDocumentationAsync<T>() where T : BaseConfiguration
        {
            var sb = new StringBuilder();
            var type = typeof(T);

            // Get schema version and attributes
            var schemaAttr = type.GetCustomAttribute<SchemaVersionAttribute>();
            var version = schemaAttr?.Version ?? new Version(1, 0);
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description;

            // Generate main documentation
            sb.AppendLine($"# {type.Name} Configuration Reference");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(description))
            {
                sb.AppendLine("## Description");
                sb.AppendLine();
                sb.AppendLine(description);
                sb.AppendLine();
            }

            // Version information
            sb.AppendLine("## Version Information");
            sb.AppendLine();
            sb.AppendLine($"Current Schema Version: {version}");
            sb.AppendLine();

            // Generate environment documentation
            GenerateEnvironmentDocumentation(type, sb);

            // Generate properties documentation
            GeneratePropertiesDocumentation(type, sb);

            // Generate validation rules documentation
            GenerateValidationDocumentation(type, sb);

            // Generate migration documentation
            await GenerateMigrationDocumentationAsync(type, sb);

            // Generate security documentation
            GenerateSecurityDocumentation(type, sb);

            // Generate examples
            GenerateExamples(type, sb);

            // Save documentation
            var docPath = Path.Combine(_outputPath, $"{type.Name.ToLower()}_configuration.md");
            await File.WriteAllTextAsync(docPath, sb.ToString());
        }

        private void GenerateEnvironmentDocumentation(Type type, StringBuilder sb)
        {
            var envAttr = type.GetCustomAttribute<EnvironmentSpecificAttribute>();
            if (envAttr != null)
            {
                sb.AppendLine("## Environment Support");
                sb.AppendLine();
                sb.AppendLine("This configuration supports the following environments:");
                sb.AppendLine();

                foreach (var env in envAttr.ValidEnvironments)
                {
                    sb.AppendLine($"### {env}");
                    sb.AppendLine();
                    sb.AppendLine(_environmentDescriptions.GetValueOrDefault(env, "Custom environment"));
                    sb.AppendLine();
                }
            }
        }

        private void GeneratePropertiesDocumentation(Type type, StringBuilder sb)
        {
            sb.AppendLine("## Configuration Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Required | Reloadable | Secure | Description |");
            sb.AppendLine("|----------|------|-----------|------------|---------|-------------|");

            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null);

            foreach (var prop in properties)
            {
                var required = prop.GetCustomAttribute<RequiredAttribute>() != null;
                var reloadable = prop.GetCustomAttribute<ReloadableAttribute>() != null;
                var secure = prop.GetCustomAttribute<SecureStorageAttribute>() != null;
                var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var validations = GetValidationDescriptions(prop);

                if (validations.Any())
                {
                    description += "\n\nValidation:\n- " + string.Join("\n- ", validations);
                }

                sb.AppendLine(
                    $"| {prop.Name} | {GetFriendlyTypeName(prop.PropertyType)} | {(required ? "Yes" : "No")} | {(reloadable ? "Yes" : "No")} | {(secure ? "Yes" : "No")} | {description} |");
            }
            sb.AppendLine();
        }

        private void GenerateValidationDocumentation(Type type, StringBuilder sb)
        {
            sb.AppendLine("## Validation Rules");
            sb.AppendLine();

            // Document class-level validation
            var classValidations = type.GetCustomAttributes()
                .Where(a => a is ValidationAttribute)
                .Cast<ValidationAttribute>();

            if (classValidations.Any())
            {
                sb.AppendLine("### Class-Level Validation");
                sb.AppendLine();
                foreach (var validation in classValidations)
                {
                    sb.AppendLine($"- {GetValidationDescription(validation)}");
                }
                sb.AppendLine();
            }

            // Document property-level validation
            sb.AppendLine("### Property Validation Rules");
            sb.AppendLine();

            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttributes().OfType<ValidationAttribute>().Any());

            foreach (var prop in properties)
            {
                sb.AppendLine($"#### {prop.Name}");
                sb.AppendLine();
                foreach (var validation in prop.GetCustomAttributes().OfType<ValidationAttribute>())
                {
                    sb.AppendLine($"- {GetValidationDescription(validation)}");
                }
                sb.AppendLine();
            }
        }

        private async Task GenerateMigrationDocumentationAsync(Type type, StringBuilder sb)
        {
            sb.AppendLine("## Version History and Migrations");
            sb.AppendLine();

            var migrations = _migrationEngine._migrations
                .Where(m => m.Key == type)
                .SelectMany(m => m.Value)
                .OrderBy(m => m.FromVersion)
                .ToList();

            if (!migrations.Any())
            {
                sb.AppendLine("No migration history available.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("| From Version | To Version | Changes |");
            sb.AppendLine("|--------------|------------|----------|");

            foreach (var migration in migrations)
            {
                var changes = GetMigrationChanges(migration);
                sb.AppendLine($"| {migration.FromVersion} | {migration.ToVersion} | {changes} |");
            }
            sb.AppendLine();
        }

        private void GenerateSecurityDocumentation(Type type, StringBuilder sb)
        {
            sb.AppendLine("## Security Considerations");
            sb.AppendLine();

            var secureProps = type.GetProperties()
                .Where(p => p.GetCustomAttribute<SecureStorageAttribute>() != null);

            if (secureProps.Any())
            {
                sb.AppendLine("### Secure Properties");
                sb.AppendLine();
                sb.AppendLine("The following properties are stored securely with encryption:");
                sb.AppendLine();

                foreach (var prop in secureProps)
                {
                    var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    sb.AppendLine($"- **{prop.Name}**: {description ?? "Stored with encryption"}");
                }
                sb.AppendLine();
            }

            // Document encryption method
            sb.AppendLine("### Encryption Details");
            sb.AppendLine();
            sb.AppendLine("- Sensitive values are encrypted using AES-256");
            sb.AppendLine("- Encryption keys are protected using Windows DPAPI");
            sb.AppendLine("- Optional certificate-based protection for high-security environments");
            sb.AppendLine("- Key rotation is supported and recommended every 90 days");
            sb.AppendLine();
        }

        private void GenerateExamples(Type type, StringBuilder sb)
        {
            sb.AppendLine("## Configuration Examples");
            sb.AppendLine();

            // Generate example for each environment
            var envAttr = type.GetCustomAttribute<EnvironmentSpecificAttribute>();
            if (envAttr != null)
            {
                foreach (var env in envAttr.ValidEnvironments)
                {
                    sb.AppendLine($"### {env} Environment Example");
                    sb.AppendLine();
                    sb.AppendLine("```json");
                    sb.AppendLine(GenerateEnvironmentExample(type, env));
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("### Basic Example");
                sb.AppendLine();
                sb.AppendLine("```json");
                sb.AppendLine(GenerateEnvironmentExample(type, "Development"));
                sb.AppendLine("```");
                sb.AppendLine();
            }
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

        private List<string> GetValidationDescriptions(PropertyInfo prop)
        {
            return prop.GetCustomAttributes<ValidationAttribute>()
                .Select(GetValidationDescription)
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
        }

        private string GetValidationDescription(ValidationAttribute attr)
        {
            return attr switch
            {
                RequiredAttribute => "Required value",
                RangeAttribute ra => $"Value must be between {ra.Minimum} and {ra.Maximum}",
                RegularExpressionAttribute rea => $"Must match pattern: {rea.Pattern}",
                StringLengthAttribute sla => $"Length must be between {sla.MinimumLength} and {sla.MaximumLength} characters",
                _ => attr.ErrorMessage ?? attr.GetType().Name.Replace("Attribute", "")
            };
        }

        private string GetMigrationChanges(IMigrationScript migration)
        {
            // This would be enhanced to extract actual changes from migration scripts
            return "Updated configuration structure and defaults";
        }

        private string GenerateEnvironmentExample(Type type, string environment)
        {
            var example = new Dictionary<string, object>
            {
                ["SchemaVersion"] = "1.0",
                ["Environment"] = environment,
                ["LastModified"] = DateTime.UtcNow
            };

            foreach (var prop in type.GetProperties())
            {
                if (prop.Name is not "SchemaVersion" and not "Environment" and not "LastModified")
                {
                    example[prop.Name] = GetExampleValue(prop, environment);
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(example, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private object? GetExampleValue(PropertyInfo prop, string environment)
        {
            if (prop.GetCustomAttribute<SecureStorageAttribute>() != null)
            {
                return "[ENCRYPTED]";
            }

            // Generate appropriate example values based on property type and environment
            return prop.PropertyType.Name switch
            {
                "String" => $"example_{prop.Name.ToLower()}",
                "Int32" => environment == "Production" ? 100 : 10,
                "Boolean" => true,
                "Double" => 3.14,
                "TimeSpan" => "00:05:00",
                "Version" => "1.0",
                _ => null
            };
        }
    }
}