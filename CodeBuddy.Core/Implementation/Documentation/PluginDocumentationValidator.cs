using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Validates plugin documentation completeness and quality
    /// </summary>
    public class PluginDocumentationValidator
    {
        private readonly IPluginManager _pluginManager;
        private readonly XmlDocumentationParser _xmlParser;
        private readonly DocumentationAnalyzer _docAnalyzer;
        private readonly UsageExampleGenerator _exampleGenerator;
        private readonly DocumentationCoverageConfig _config;

        public PluginDocumentationValidator(
            IPluginManager pluginManager,
            XmlDocumentationParser xmlParser,
            DocumentationAnalyzer docAnalyzer,
            UsageExampleGenerator exampleGenerator,
            DocumentationCoverageConfig config)
        {
            _pluginManager = pluginManager;
            _xmlParser = xmlParser;
            _docAnalyzer = docAnalyzer;
            _exampleGenerator = exampleGenerator;
            _config = config;
        }

        public async Task<PluginDocumentationReport> ValidatePluginDocumentationAsync()
        {
            var report = new PluginDocumentationReport();

            try
            {
                var plugins = await _pluginManager.GetAvailablePluginsAsync();
                
                foreach (var plugin in plugins)
                {
                    var pluginValidation = await ValidatePluginAsync(plugin);
                    report.PluginValidations.Add(pluginValidation);
                }

                // Calculate overall metrics
                report.CalculateOverallMetrics();

                // Generate recommendations
                report.Recommendations.AddRange(GeneratePluginRecommendations(report));

                report.Success = true;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Error = ex.Message;
            }

            return report;
        }

        private async Task<PluginValidationResult> ValidatePluginAsync(IPlugin plugin)
        {
            var result = new PluginValidationResult
            {
                PluginName = plugin.Name,
                Version = plugin.Version
            };

            // Validate basic plugin documentation
            result.HasDescription = !string.IsNullOrEmpty(plugin.Description);
            result.HasVersion = !string.IsNullOrEmpty(plugin.Version);
            
            // Validate plugin type documentation
            var pluginType = plugin.GetType();
            var typeDoc = _xmlParser.GetTypeDocumentation(pluginType);
            result.HasTypeDocumentation = !string.IsNullOrEmpty(typeDoc);

            // Validate configuration documentation
            var config = await _pluginManager.GetPluginConfigurationAsync(plugin.Name);
            if (config != null)
            {
                result.ConfigurationDocumentation = ValidateConfigurationDocumentation(config);
            }

            // Validate interfaces and extension points
            result.InterfaceDocumentation = ValidatePluginInterfaces(plugin);

            // Validate examples
            var examples = await _exampleGenerator.GeneratePluginExamples(plugin);
            result.HasExamples = examples.Any();
            result.ExampleCount = examples.Count;

            // Validate dependencies
            result.DependencyDocumentation = ValidatePluginDependencies(plugin.Dependencies);

            // Calculate completeness score
            result.CalculateCompletenessScore();

            return result;
        }

        private ConfigurationDocumentationResult ValidateConfigurationDocumentation(object config)
        {
            var result = new ConfigurationDocumentationResult();
            
            var configType = config.GetType();
            var properties = configType.GetProperties();

            foreach (var prop in properties)
            {
                var propDoc = new PropertyDocumentationInfo
                {
                    PropertyName = prop.Name,
                    HasDescription = false,
                    HasExample = false,
                    HasDefaultValue = false
                };

                // Check XML documentation
                var xmlDoc = _xmlParser.GetPropertyDocumentation(prop);
                if (xmlDoc != null)
                {
                    propDoc.HasDescription = !string.IsNullOrEmpty(xmlDoc.Summary);
                    propDoc.HasExample = xmlDoc.Examples.Any();
                }

                // Check default value attribute
                var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
                propDoc.HasDefaultValue = defaultValueAttr != null;

                result.PropertyDocumentation.Add(propDoc);
            }

            result.CalculateCompleteness();
            return result;
        }

        private InterfaceDocumentationResult ValidatePluginInterfaces(IPlugin plugin)
        {
            var result = new InterfaceDocumentationResult();
            var pluginType = plugin.GetType();

            foreach (var iface in pluginType.GetInterfaces())
            {
                if (!iface.FullName.StartsWith("CodeBuddy")) continue;

                var interfaceDoc = new InterfaceDocumentationInfo
                {
                    InterfaceName = iface.Name,
                    HasDocumentation = false,
                    MethodsDocumented = 0,
                    TotalMethods = 0
                };

                // Check interface documentation
                var xmlDoc = _xmlParser.GetTypeDocumentation(iface);
                interfaceDoc.HasDocumentation = !string.IsNullOrEmpty(xmlDoc);

                // Check methods
                var methods = iface.GetMethods();
                interfaceDoc.TotalMethods = methods.Length;
                interfaceDoc.MethodsDocumented = methods.Count(m => 
                    !string.IsNullOrEmpty(_xmlParser.GetMethodDocumentation(m)?.Summary));

                result.InterfaceDocumentation.Add(interfaceDoc);
            }

            result.CalculateCompleteness();
            return result;
        }

        private DependencyDocumentationResult ValidatePluginDependencies(IEnumerable<string> dependencies)
        {
            var result = new DependencyDocumentationResult();

            foreach (var dep in dependencies)
            {
                var depDoc = new DependencyDocumentationInfo
                {
                    DependencyName = dep,
                    HasVersionConstraint = dep.Contains("@"),
                    HasDocumentation = false
                };

                // Look for dependency documentation in plugin's XML docs
                var xmlDoc = _xmlParser.GetDependencyDocumentation(dep);
                depDoc.HasDocumentation = !string.IsNullOrEmpty(xmlDoc);

                result.DependencyDocumentation.Add(depDoc);
            }

            result.CalculateCompleteness();
            return result;
        }

        private List<DocumentationRecommendation> GeneratePluginRecommendations(PluginDocumentationReport report)
        {
            var recommendations = new List<DocumentationRecommendation>();

            foreach (var validation in report.PluginValidations.Where(v => v.CompletenessScore < _config.Thresholds.MinimumPluginDocumentationCompleteness))
            {
                var plugin = validation;
                var rec = new DocumentationRecommendation
                {
                    Area = "Plugin Documentation",
                    Priority = RecommendationPriority.High,
                    Description = $"Improve documentation completeness for plugin '{plugin.PluginName}' (current: {plugin.CompletenessScore:P0})",
                    Impact = "Better plugin usability and integration experience"
                };

                if (!plugin.HasTypeDocumentation)
                {
                    rec.Details.Add("Add XML documentation for the plugin class");
                }

                if (!plugin.HasExamples)
                {
                    rec.Details.Add("Add usage examples");
                }

                if (plugin.ConfigurationDocumentation?.Completeness < 1.0)
                {
                    rec.Details.Add("Improve configuration property documentation");
                }

                if (plugin.InterfaceDocumentation?.Completeness < 1.0)
                {
                    rec.Details.Add("Complete interface implementation documentation");
                }

                if (plugin.DependencyDocumentation?.Completeness < 1.0)
                {
                    rec.Details.Add("Add dependency documentation and version constraints");
                }

                recommendations.Add(rec);
            }

            return recommendations;
        }
    }
}