using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
        private const string DocumentationHistoryPath = "docs/configuration/history";
        private readonly string _currentVersion;
        
        public ConfigurationDocumentationGenerator(
            ILogger<ConfigurationDocumentationGenerator> logger,
            string currentVersion,
            params Assembly[] assemblies)
        {
            _logger = logger;
            _assemblies = assemblies;
            _currentVersion = currentVersion;
        }

        /// <summary>
        /// Validates an existing configuration against the documentation rules
        /// </summary>
        public async Task<ValidationResult> ValidateConfiguration(string configurationJson)
        {
            try
            {
                var result = new ValidationResult();
                var configTypes = GetConfigurationTypes();
                
                foreach (var type in configTypes)
                {
                    var section = JsonSerializer.Deserialize(configurationJson, type);
                    var validationResults = new List<ValidationResult>();
                    
                    if (!Validator.TryValidateObject(section, new ValidationContext(section), validationResults, true))
                    {
                        foreach (var validationResult in validationResults)
                        {
                            result.ValidationErrors.Add(validationResult);
                        }
                    }

                    ValidateSchemaVersion(type, section, result);
                    ValidateRequiredProperties(type, section, result);
                    ValidatePropertyRanges(type, section, result);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration");
                throw;
            }
        }

        /// <summary>
        /// Archives the current documentation and maintains version history
        /// </summary>
        public async Task ArchiveDocumentationVersion(string documentation)
        {
            try
            {
                var versionPath = Path.Combine(DocumentationHistoryPath, _currentVersion);
                Directory.CreateDirectory(versionPath);

                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var fileName = Path.Combine(versionPath, $"configuration_{timestamp}.md");

                await File.WriteAllTextAsync(fileName, documentation);

                // Create version index
                var indexPath = Path.Combine(DocumentationHistoryPath, "index.md");
                var indexContent = new StringBuilder();
                
                indexContent.AppendLine("# Configuration Documentation Version History");
                indexContent.AppendLine();
                
                foreach (var dir in Directory.GetDirectories(DocumentationHistoryPath))
                {
                    var version = Path.GetFileName(dir);
                    var files = Directory.GetFiles(dir, "*.md")
                        .OrderByDescending(f => f)
                        .Select(f => Path.GetFileName(f));

                    indexContent.AppendLine($"## Version {version}");
                    indexContent.AppendLine();
                    
                    foreach (var file in files)
                    {
                        indexContent.AppendLine($"- [{file}]({version}/{file})");
                    }
                    indexContent.AppendLine();
                }

                await File.WriteAllTextAsync(indexPath, indexContent.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving documentation version");
                throw;
            }
        }

        private void ValidateSchemaVersion(Type type, object section, ValidationResult result)
        {
            var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>();
            if (schemaVersion != null)
            {
                var sectionVersion = section.GetType().GetCustomAttribute<SchemaVersionAttribute>();
                if (sectionVersion == null || sectionVersion.Version != schemaVersion.Version)
                {
                    result.ValidationErrors.Add(
                        new ValidationResult($"Schema version mismatch. Expected {schemaVersion.Version}"));
                }
            }
        }

        private void ValidateRequiredProperties(Type type, object section, ValidationResult result)
        {
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() != null);

            foreach (var property in properties)
            {
                var value = property.GetValue(section);
                if (value == null)
                {
                    result.ValidationErrors.Add(
                        new ValidationResult($"Required property {property.Name} is missing"));
                }
            }
        }

        private void ValidatePropertyRanges(Type type, object section, ValidationResult result)
        {
            var properties = type.GetProperties()
                .Where(p => p.GetCustomAttribute<RangeAttribute>() != null);

            foreach (var property in properties)
            {
                var value = property.GetValue(section);
                var rangeAttr = property.GetCustomAttribute<RangeAttribute>();

                if (value != null && !rangeAttr.IsValid(value))
                {
                    result.ValidationErrors.Add(
                        new ValidationResult(
                            $"Property {property.Name} value {value} is outside valid range {rangeAttr.Minimum} to {rangeAttr.Maximum}"));
                }
            }
        }

        public ConfigurationDocumentationGenerator(
            ILogger<ConfigurationDocumentationGenerator> logger,
            params Assembly[] assemblies)
        {
            _logger = logger;
            _assemblies = assemblies;
        }

        /// <summary>
        /// Generates markdown documentation for all configuration sections including examples,
        /// migration paths, and validation rules
        /// </summary>
        public async Task<string> GenerateDocumentation(bool archive = true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Configuration Documentation");
                sb.AppendLine();
                sb.AppendLine("This document describes all available configuration options.");
                sb.AppendLine();

                var configTypes = GetConfigurationTypes();

                // Table of Contents
                sb.AppendLine("## Table of Contents");
                sb.AppendLine();
                foreach (var type in configTypes)
                {
                    sb.AppendLine($"- [{type.Name}](#{type.Name.ToLower()})");
                }
                sb.AppendLine();

                // Common Configuration Scenarios
                sb.AppendLine("## Common Configuration Scenarios");
                sb.AppendLine();
                DocumentCommonScenarios(sb);

                // Configuration Migration Guide
                sb.AppendLine("## Configuration Migration Guide");
                sb.AppendLine();
                DocumentMigrationPaths(sb, configTypes);

                // Environment-Specific Configuration
                sb.AppendLine("## Environment-Specific Configuration");
                sb.AppendLine();
                DocumentEnvironmentOverrides(sb);

                // Configuration Sections
                foreach (var type in configTypes)
                {
                    DocumentConfigurationType(sb, type);
                }

                // Configuration Templates
                sb.AppendLine("## Configuration Templates");
                sb.AppendLine();
                GenerateConfigurationTemplates(sb, configTypes);

                var documentation = sb.ToString();
                
                if (archive)
                {
                    await ArchiveDocumentationVersion(documentation);
                }
                
                return documentation;
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
            var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>();
            
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
                sb.AppendLine($"**Section:** {sectionAttr.Name}");
                sb.AppendLine();
            }
            
            if (schemaVersion != null)
            {
                sb.AppendLine($"**Schema Version:** {schemaVersion.Version}");
                sb.AppendLine();
            }

            // Add code example
            sb.AppendLine("### Example Configuration");
            sb.AppendLine("```json");
            sb.AppendLine(GenerateExampleJson(type));
            sb.AppendLine("```");
            sb.AppendLine();

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

        private void DocumentCommonScenarios(StringBuilder sb)
        {
            sb.AppendLine("Here are some common configuration scenarios and how to implement them:");
            sb.AppendLine();
            
            // Development Environment
            sb.AppendLine("### Development Environment Setup");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"Logging\": {");
            sb.AppendLine("    \"Level\": \"Debug\",");
            sb.AppendLine("    \"EnableConsole\": true");
            sb.AppendLine("  },");
            sb.AppendLine("  \"Cache\": {");
            sb.AppendLine("    \"InMemory\": true,");
            sb.AppendLine("    \"TTLMinutes\": 5");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            // Production Environment
            sb.AppendLine("### Production Environment Setup");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"Logging\": {");
            sb.AppendLine("    \"Level\": \"Warning\",");
            sb.AppendLine("    \"EnableConsole\": false,");
            sb.AppendLine("    \"LogToFile\": true");
            sb.AppendLine("  },");
            sb.AppendLine("  \"Cache\": {");
            sb.AppendLine("    \"Distributed\": true,");
            sb.AppendLine("    \"TTLMinutes\": 60");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
        }

        private void DocumentMigrationPaths(StringBuilder sb, IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>();
                var migrations = type.GetCustomAttributes<ConfigurationMigrationAttribute>();

                if (schemaVersion != null && migrations.Any())
                {
                    sb.AppendLine($"### {type.Name}");
                    sb.AppendLine();
                    
                    foreach (var migration in migrations.OrderBy(m => m.FromVersion))
                    {
                        sb.AppendLine($"#### {migration.FromVersion} → {migration.ToVersion}");
                        sb.AppendLine();
                        sb.AppendLine(migration.Description);
                        sb.AppendLine();
                        
                        if (!string.IsNullOrEmpty(migration.CodeExample))
                        {
                            sb.AppendLine("Example:");
                            sb.AppendLine("```json");
                            sb.AppendLine(migration.CodeExample);
                            sb.AppendLine("```");
                            sb.AppendLine();
                        }
                    }
                }
            }
        }

        private void DocumentEnvironmentOverrides(StringBuilder sb)
        {
            sb.AppendLine("Configuration values can be overridden based on the environment using:");
            sb.AppendLine();
            sb.AppendLine("1. Environment Variables");
            sb.AppendLine("   ```");
            sb.AppendLine("   CODEBUDDY_Logging__Level=Debug");
            sb.AppendLine("   CODEBUDDY_Cache__TTLMinutes=30");
            sb.AppendLine("   ```");
            sb.AppendLine();
            sb.AppendLine("2. Environment-Specific JSON Files");
            sb.AppendLine("   ```");
            sb.AppendLine("   appsettings.Development.json");
            sb.AppendLine("   appsettings.Production.json");
            sb.AppendLine("   appsettings.Staging.json");
            sb.AppendLine("   ```");
        }

        private string GenerateExampleJson(Type type)
        {
            var example = Activator.CreateInstance(type);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(example, type, options);
        }

        private void GenerateConfigurationTemplates(StringBuilder sb, IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                sb.AppendLine($"### {type.Name} Template");
                sb.AppendLine("```json");
                sb.AppendLine(GenerateExampleJson(type));
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }
    }
}