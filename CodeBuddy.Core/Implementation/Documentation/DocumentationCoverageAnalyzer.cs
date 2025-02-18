using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Analyzes and enforces documentation coverage requirements for the codebase
    /// </summary>
    public class DocumentationCoverageAnalyzer
    {
        private readonly DocumentationRequirements _requirements;
        private readonly XmlDocumentationParser _xmlParser;

        public DocumentationCoverageAnalyzer(DocumentationRequirements requirements = null)
        {
            _requirements = requirements ?? new DocumentationRequirements();
            _xmlParser = new XmlDocumentationParser();
        }

        /// <summary>
        /// Analyzes documentation coverage for all assemblies in the codebase
        /// </summary>
        public async Task<DocumentationCoverageReport> AnalyzeCoverageAsync(IEnumerable<Assembly> assemblies)
        {
            var report = new DocumentationCoverageReport();
            var totalTypes = 0;
            var documentedTypes = 0;

            foreach (var assembly in assemblies.Where(a => a.FullName.StartsWith("CodeBuddy")))
            {
                var types = assembly.GetTypes().Where(t => t.IsPublic);
                foreach (var type in types)
                {
                    totalTypes++;
                    var typeCoverage = await AnalyzeTypeCoverageAsync(type);
                    report.Types.Add(typeCoverage);

                    if (typeCoverage.TypeCoverage >= _requirements.MinimumTypeCoverage)
                        documentedTypes++;

                    // Add any issues found
                    AddCoverageIssues(report.Issues, typeCoverage);
                }
            }

            // Calculate overall coverage
            report.OverallCoverage = totalTypes > 0 ? (documentedTypes * 100.0) / totalTypes : 0;
            report.MeetsThreshold = report.OverallCoverage >= _requirements.MinimumOverallCoverage;

            // Generate recommendations
            report.Recommendations.AddRange(GenerateRecommendations(report));

            return report;
        }

        private async Task<TypeCoverageInfo> AnalyzeTypeCoverageAsync(Type type)
        {
            var coverage = new TypeCoverageInfo
            {
                TypeName = type.Name,
                Namespace = type.Namespace
            };

            // Analyze type documentation
            var typeDoc = await _xmlParser.GetTypeDocumentationAsync(type);
            coverage.TypeCoverage = CalculateTypeCoverage(type, typeDoc);

            // Analyze methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName);
            var methodCoverage = await AnalyzeMethodsAsync(methods);
            coverage.MethodCoverage = methodCoverage.coverage;
            coverage.MissingDocumentation.AddRange(methodCoverage.missing);

            // Analyze properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var propertyCoverage = await AnalyzePropertiesAsync(properties);
            coverage.PropertyCoverage = propertyCoverage.coverage;
            coverage.MissingDocumentation.AddRange(propertyCoverage.missing);

            return coverage;
        }

        private async Task<(double coverage, List<MemberCoverageInfo> missing)> AnalyzeMethodsAsync(IEnumerable<MethodInfo> methods)
        {
            var totalMethods = 0;
            var documentedMethods = 0;
            var missingDocs = new List<MemberCoverageInfo>();

            foreach (var method in methods)
            {
                totalMethods++;
                var methodDoc = await _xmlParser.GetMethodDocumentationAsync(method);
                var missing = new List<string>();

                // Check summary
                if (string.IsNullOrEmpty(methodDoc?.Summary))
                    missing.Add("summary");

                // Check parameters
                if (_requirements.RequireParameterDocs)
                {
                    var parameters = method.GetParameters();
                    foreach (var param in parameters)
                    {
                        if (!methodDoc?.Parameters.Any(p => p.Name == param.Name) ?? true)
                            missing.Add($"parameter '{param.Name}'");
                    }
                }

                // Check return value
                if (_requirements.RequireReturnDocs && method.ReturnType != typeof(void))
                {
                    if (string.IsNullOrEmpty(methodDoc?.Returns))
                        missing.Add("return value");
                }

                // Check exceptions
                if (_requirements.RequireExceptionDocs)
                {
                    var thrownExceptions = method.GetCustomAttributes<ThrowsAttribute>()
                        .Select(a => a.ExceptionType.Name);
                    foreach (var ex in thrownExceptions)
                    {
                        if (!methodDoc?.Exceptions.Any(e => e.Type == ex) ?? true)
                            missing.Add($"exception '{ex}'");
                    }
                }

                if (missing.Count == 0)
                    documentedMethods++;
                else
                    missingDocs.Add(new MemberCoverageInfo
                    {
                        MemberName = method.Name,
                        MemberType = "Method",
                        MissingElements = missing,
                        Severity = _requirements.MissingDocumentationSeverity
                    });
            }

            return (totalMethods > 0 ? (documentedMethods * 100.0) / totalMethods : 0, missingDocs);
        }

        private async Task<(double coverage, List<MemberCoverageInfo> missing)> AnalyzePropertiesAsync(IEnumerable<PropertyInfo> properties)
        {
            var totalProps = 0;
            var documentedProps = 0;
            var missingDocs = new List<MemberCoverageInfo>();

            foreach (var prop in properties)
            {
                totalProps++;
                var propDoc = await _xmlParser.GetPropertyDocumentationAsync(prop);
                var missing = new List<string>();

                if (string.IsNullOrEmpty(propDoc?.Summary))
                    missing.Add("summary");

                if (missing.Count == 0)
                    documentedProps++;
                else
                    missingDocs.Add(new MemberCoverageInfo
                    {
                        MemberName = prop.Name,
                        MemberType = "Property",
                        MissingElements = missing,
                        Severity = _requirements.MissingDocumentationSeverity
                    });
            }

            return (totalProps > 0 ? (documentedProps * 100.0) / totalProps : 0, missingDocs);
        }

        private double CalculateTypeCoverage(Type type, TypeDocumentation typeDoc)
        {
            var elements = new List<bool>
            {
                !string.IsNullOrEmpty(typeDoc?.Summary),
                !string.IsNullOrEmpty(typeDoc?.Description)
            };

            if (_requirements.RequireExamples)
                elements.Add(typeDoc?.Examples?.Any() ?? false);

            return (elements.Count(e => e) * 100.0) / elements.Count;
        }

        private void AddCoverageIssues(List<DocumentationIssue> issues, TypeCoverageInfo typeCoverage)
        {
            if (typeCoverage.TypeCoverage < _requirements.MinimumTypeCoverage)
            {
                issues.Add(new DocumentationIssue
                {
                    Component = $"{typeCoverage.Namespace}.{typeCoverage.TypeName}",
                    IssueType = "InsufficientTypeCoverage",
                    Description = $"Type documentation coverage ({typeCoverage.TypeCoverage:F1}%) is below the required threshold ({_requirements.MinimumTypeCoverage}%)",
                    Severity = _requirements.MissingDocumentationSeverity
                });
            }

            if (typeCoverage.MethodCoverage < _requirements.MinimumMethodCoverage)
            {
                issues.Add(new DocumentationIssue
                {
                    Component = $"{typeCoverage.Namespace}.{typeCoverage.TypeName}",
                    IssueType = "InsufficientMethodCoverage",
                    Description = $"Method documentation coverage ({typeCoverage.MethodCoverage:F1}%) is below the required threshold ({_requirements.MinimumMethodCoverage}%)",
                    Severity = _requirements.MissingDocumentationSeverity
                });
            }

            if (typeCoverage.PropertyCoverage < _requirements.MinimumPropertyCoverage)
            {
                issues.Add(new DocumentationIssue
                {
                    Component = $"{typeCoverage.Namespace}.{typeCoverage.TypeName}",
                    IssueType = "InsufficientPropertyCoverage",
                    Description = $"Property documentation coverage ({typeCoverage.PropertyCoverage:F1}%) is below the required threshold ({_requirements.MinimumPropertyCoverage}%)",
                    Severity = _requirements.MissingDocumentationSeverity
                });
            }

            foreach (var missing in typeCoverage.MissingDocumentation)
            {
                issues.Add(new DocumentationIssue
                {
                    Component = $"{typeCoverage.Namespace}.{typeCoverage.TypeName}.{missing.MemberName}",
                    IssueType = "MissingDocumentation",
                    Description = $"Missing documentation for {missing.MissingElements.Count} element(s): {string.Join(", ", missing.MissingElements)}",
                    Severity = missing.Severity
                });
            }
        }

        private List<string> GenerateRecommendations(DocumentationCoverageReport report)
        {
            var recommendations = new List<string>();

            if (report.OverallCoverage < _requirements.MinimumOverallCoverage)
            {
                recommendations.Add($"Overall documentation coverage ({report.OverallCoverage:F1}%) needs improvement to meet the minimum requirement of {_requirements.MinimumOverallCoverage}%");
            }

            var typesByNamespace = report.Types
                .GroupBy(t => t.Namespace)
                .Where(g => g.Any(t => t.TypeCoverage < _requirements.MinimumTypeCoverage));

            foreach (var ns in typesByNamespace)
            {
                var undocumentedTypes = ns.Where(t => t.TypeCoverage < _requirements.MinimumTypeCoverage)
                    .Select(t => t.TypeName);
                recommendations.Add($"Improve documentation coverage for types in {ns.Key}: {string.Join(", ", undocumentedTypes)}");
            }

            var commonIssues = report.Issues
                .GroupBy(i => i.IssueType)
                .OrderByDescending(g => g.Count());

            foreach (var issue in commonIssues)
            {
                recommendations.Add($"Found {issue.Count()} {issue.Key} issues. Focus on documenting {string.Join(", ", issue.Select(i => i.Component).Distinct().Take(3))}");
            }

            return recommendations;
        }
    }
}