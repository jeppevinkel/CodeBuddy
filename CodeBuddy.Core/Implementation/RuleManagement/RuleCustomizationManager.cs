using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Rules;

namespace CodeBuddy.Core.Implementation.RuleManagement
{
    /// <summary>
    /// Manages the customization, versioning, and validation of rule templates.
    /// </summary>
    public class RuleCustomizationManager
    {
        private readonly Dictionary<string, RuleTemplate> _templates;
        private readonly Dictionary<string, List<CustomRule>> _versionHistory;
        private readonly IRuleManager _ruleManager;
        private readonly string _ruleStoragePath;

        public RuleCustomizationManager(IRuleManager ruleManager, string ruleStoragePath)
        {
            _templates = new Dictionary<string, RuleTemplate>();
            _versionHistory = new Dictionary<string, List<CustomRule>>();
            _ruleManager = ruleManager;
            _ruleStoragePath = ruleStoragePath;
        }

        /// <summary>
        /// Creates a new rule template with customizable parameters.
        /// </summary>
        public RuleTemplate CreateTemplate(string templateId, string baseRuleId = null)
        {
            var template = new RuleTemplate
            {
                TemplateId = templateId,
                BaseRuleId = baseRuleId,
                Parameters = new Dictionary<string, ParameterDefinition>(),
                ValidationRules = new List<string>(),
                Documentation = new RuleDocumentation()
            };

            if (baseRuleId != null)
            {
                var baseRule = _ruleManager.GetRule(baseRuleId);
                if (baseRule == null)
                    throw new ArgumentException($"Base rule {baseRuleId} not found");

                template.InheritFrom(baseRule);
            }

            _templates[templateId] = template;
            return template;
        }

        /// <summary>
        /// Instantiates a custom rule from a template with provided parameters.
        /// </summary>
        public CustomRule InstantiateRule(string templateId, Dictionary<string, object> parameters)
        {
            if (!_templates.TryGetValue(templateId, out var template))
                throw new ArgumentException($"Template {templateId} not found");

            var rule = template.CreateRule(parameters);
            ValidateParameters(template, parameters);

            var ruleId = $"{templateId}-{Guid.NewGuid()}";
            rule.Id = ruleId;
            rule.Version = new Version(1, 0, 0);

            TrackVersion(rule);
            _ruleManager.RegisterRule(rule);

            return rule;
        }

        /// <summary>
        /// Exports rule templates to JSON/YAML format.
        /// </summary>
        public async Task ExportTemplates(string format = "json")
        {
            var exportPath = Path.Combine(_ruleStoragePath, "templates");
            Directory.CreateDirectory(exportPath);

            foreach (var template in _templates.Values)
            {
                var fileName = Path.Combine(exportPath, $"{template.TemplateId}.{format}");
                var content = format.ToLower() == "json" 
                    ? JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true })
                    : ConvertToYaml(template);

                await File.WriteAllTextAsync(fileName, content);
            }
        }

        /// <summary>
        /// Imports rule templates from JSON/YAML files.
        /// </summary>
        public async Task ImportTemplates(string format = "json")
        {
            var importPath = Path.Combine(_ruleStoragePath, "templates");
            if (!Directory.Exists(importPath))
                return;

            var files = Directory.GetFiles(importPath, $"*.{format}");
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                RuleTemplate template;

                if (format.ToLower() == "json")
                    template = JsonSerializer.Deserialize<RuleTemplate>(content);
                else
                    template = ParseYaml(content);

                _templates[template.TemplateId] = template;
            }
        }

        /// <summary>
        /// Validates template parameters against their definitions.
        /// </summary>
        private void ValidateParameters(RuleTemplate template, Dictionary<string, object> parameters)
        {
            foreach (var param in template.Parameters)
            {
                if (param.Value.Required && !parameters.ContainsKey(param.Key))
                    throw new ArgumentException($"Required parameter {param.Key} not provided");

                if (parameters.TryGetValue(param.Key, out var value))
                {
                    if (!ValidateParameterType(value, param.Value.Type))
                        throw new ArgumentException($"Invalid type for parameter {param.Key}");

                    if (!ValidateParameterConstraints(value, param.Value.Constraints))
                        throw new ArgumentException($"Parameter {param.Key} fails validation constraints");
                }
            }
        }

        /// <summary>
        /// Validates parameter value against expected type.
        /// </summary>
        private bool ValidateParameterType(object value, string expectedType)
        {
            return expectedType.ToLower() switch
            {
                "string" => value is string,
                "number" => value is int or double or float,
                "boolean" => value is bool,
                "array" => value is System.Collections.IEnumerable,
                "object" => value is System.Collections.IDictionary,
                _ => false
            };
        }

        /// <summary>
        /// Validates parameter value against defined constraints.
        /// </summary>
        private bool ValidateParameterConstraints(object value, Dictionary<string, object> constraints)
        {
            if (constraints == null)
                return true;

            foreach (var constraint in constraints)
            {
                switch (constraint.Key)
                {
                    case "minLength" when value is string s:
                        if (s.Length < Convert.ToInt32(constraint.Value))
                            return false;
                        break;
                    case "maxLength" when value is string s:
                        if (s.Length > Convert.ToInt32(constraint.Value))
                            return false;
                        break;
                    case "pattern" when value is string s:
                        if (!System.Text.RegularExpressions.Regex.IsMatch(s, constraint.Value.ToString()))
                            return false;
                        break;
                    case "minimum" when value is IComparable c:
                        if (c.CompareTo(Convert.ChangeType(constraint.Value, c.GetType())) < 0)
                            return false;
                        break;
                    case "maximum" when value is IComparable c:
                        if (c.CompareTo(Convert.ChangeType(constraint.Value, c.GetType())) > 0)
                            return false;
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Tracks version history of a rule.
        /// </summary>
        private void TrackVersion(CustomRule rule)
        {
            if (!_versionHistory.ContainsKey(rule.Id))
                _versionHistory[rule.Id] = new List<CustomRule>();

            _versionHistory[rule.Id].Add(rule.Clone());
        }

        /// <summary>
        /// Updates a rule while maintaining version history.
        /// </summary>
        public void UpdateRule(string ruleId, Action<CustomRule> updateAction)
        {
            var rule = _ruleManager.GetRule(ruleId);
            if (rule == null)
                throw new ArgumentException($"Rule {ruleId} not found");

            var newRule = rule.Clone();
            updateAction(newRule);

            newRule.Version = new Version(
                rule.Version.Major,
                rule.Version.Minor,
                rule.Version.Build + 1
            );

            TrackVersion(newRule);
            _ruleManager.RegisterRule(newRule);
        }

        /// <summary>
        /// Generates documentation for a rule template.
        /// </summary>
        public string GenerateDocumentation(string templateId)
        {
            if (!_templates.TryGetValue(templateId, out var template))
                throw new ArgumentException($"Template {templateId} not found");

            return template.Documentation.GenerateMarkdown();
        }

        /// <summary>
        /// Analyzes performance impact of a rule template.
        /// </summary>
        public PerformanceAnalysis AnalyzePerformance(string templateId)
        {
            if (!_templates.TryGetValue(templateId, out var template))
                throw new ArgumentException($"Template {templateId} not found");

            return new PerformanceAnalysis
            {
                TemplateId = templateId,
                EstimatedImpact = template.PerformanceImpact,
                ResourceUsage = CalculateResourceUsage(template),
                Recommendations = GenerateOptimizationRecommendations(template)
            };
        }

        private Dictionary<string, double> CalculateResourceUsage(RuleTemplate template)
        {
            // Calculate estimated resource usage based on rule complexity
            return new Dictionary<string, double>
            {
                ["cpu"] = template.Complexity * 0.1,
                ["memory"] = template.Complexity * 5,
                ["io"] = template.Complexity * 2
            };
        }

        private List<string> GenerateOptimizationRecommendations(RuleTemplate template)
        {
            var recommendations = new List<string>();

            if (template.Complexity > 7)
                recommendations.Add("Consider breaking down the rule into smaller, more focused rules");

            if (template.PerformanceImpact > 8)
                recommendations.Add("Review rule logic for potential optimization opportunities");

            if (template.Dependencies.Count > 5)
                recommendations.Add("High number of dependencies may impact performance. Consider reducing dependencies.");

            return recommendations;
        }

        private string ConvertToYaml(RuleTemplate template)
        {
            // Basic YAML conversion implementation
            var yaml = $"templateId: {template.TemplateId}\n";
            yaml += $"baseRuleId: {template.BaseRuleId}\n";
            yaml += "parameters:\n";
            
            foreach (var param in template.Parameters)
            {
                yaml += $"  {param.Key}:\n";
                yaml += $"    type: {param.Value.Type}\n";
                yaml += $"    required: {param.Value.Required.ToString().ToLower()}\n";
                if (param.Value.Constraints?.Any() == true)
                {
                    yaml += "    constraints:\n";
                    foreach (var constraint in param.Value.Constraints)
                    {
                        yaml += $"      {constraint.Key}: {constraint.Value}\n";
                    }
                }
            }

            return yaml;
        }

        private RuleTemplate ParseYaml(string yaml)
        {
            // Basic YAML parsing implementation
            var template = new RuleTemplate();
            var lines = yaml.Split('\n');
            
            foreach (var line in lines)
            {
                var parts = line.Trim().Split(':');
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "templateId":
                        template.TemplateId = value;
                        break;
                    case "baseRuleId":
                        template.BaseRuleId = value;
                        break;
                }
            }

            return template;
        }
    }

    public class RuleTemplate
    {
        public string TemplateId { get; set; }
        public string BaseRuleId { get; set; }
        public Dictionary<string, ParameterDefinition> Parameters { get; set; }
        public List<string> ValidationRules { get; set; }
        public RuleDocumentation Documentation { get; set; }
        public int Complexity { get; set; }
        public int PerformanceImpact { get; set; }
        public HashSet<string> Dependencies { get; set; } = new();

        public void InheritFrom(CustomRule baseRule)
        {
            Complexity = baseRule.PerformanceImpact;
            PerformanceImpact = baseRule.PerformanceImpact;
            Dependencies = new HashSet<string>(baseRule.Dependencies);
            Documentation.InheritFrom(baseRule.Documentation);
        }

        public CustomRule CreateRule(Dictionary<string, object> parameters)
        {
            return new CustomRule
            {
                Configuration = parameters,
                PerformanceImpact = PerformanceImpact,
                Dependencies = Dependencies,
                Documentation = Documentation.Content
            };
        }
    }

    public class ParameterDefinition
    {
        public string Type { get; set; }
        public bool Required { get; set; }
        public Dictionary<string, object> Constraints { get; set; }
        public string Description { get; set; }
    }

    public class RuleDocumentation
    {
        public string Content { get; set; }
        public string Examples { get; set; }
        public List<string> SeeAlso { get; set; } = new();

        public void InheritFrom(string baseDocumentation)
        {
            Content = $"{baseDocumentation}\n\nInherited and customized from base rule.";
        }

        public string GenerateMarkdown()
        {
            var markdown = $"# Rule Documentation\n\n{Content}\n\n";
            
            if (!string.IsNullOrEmpty(Examples))
                markdown += $"## Examples\n\n{Examples}\n\n";
            
            if (SeeAlso.Any())
            {
                markdown += "## See Also\n\n";
                foreach (var reference in SeeAlso)
                {
                    markdown += $"* {reference}\n";
                }
            }

            return markdown;
        }
    }

    public class PerformanceAnalysis
    {
        public string TemplateId { get; set; }
        public int EstimatedImpact { get; set; }
        public Dictionary<string, double> ResourceUsage { get; set; }
        public List<string> Recommendations { get; set; }
    }
}