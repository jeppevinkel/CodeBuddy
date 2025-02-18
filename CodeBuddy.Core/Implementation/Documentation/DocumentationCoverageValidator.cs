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
    /// Validates documentation coverage and completeness across the codebase
    /// </summary>
    public class DocumentationCoverageValidator
    {
        private readonly XmlDocumentationParser _xmlParser;
        private readonly DocumentationAnalyzer _docAnalyzer;
        private readonly CrossReferenceGenerator _crossRefGenerator;
        private readonly UsageExampleGenerator _exampleGenerator;
        private readonly CodeExampleValidator _exampleValidator;
        private readonly DocumentationCoverageConfig _config;

        public DocumentationCoverageValidator(
            XmlDocumentationParser xmlParser,
            DocumentationAnalyzer docAnalyzer,
            CrossReferenceGenerator crossRefGenerator,
            UsageExampleGenerator exampleGenerator,
            CodeExampleValidator exampleValidator,
            DocumentationCoverageConfig config)
        {
            _xmlParser = xmlParser;
            _docAnalyzer = docAnalyzer;
            _crossRefGenerator = crossRefGenerator;
            _exampleGenerator = exampleGenerator;
            _exampleValidator = exampleValidator;
            _config = config;
        }

        public async Task<DocumentationCoverageReport> ValidateDocumentationCoverageAsync()
        {
            var report = new DocumentationCoverageReport();

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName.StartsWith("CodeBuddy"));

                foreach (var assembly in assemblies)
                {
                    var assemblyReport = await ValidateAssemblyDocumentationAsync(assembly);
                    report.AssemblyReports.Add(assemblyReport);
                }

                // Calculate overall coverage metrics
                report.CalculateOverallMetrics();

                // Validate against thresholds
                report.ValidateThresholds(_config.Thresholds);

                // Generate recommendations
                report.Recommendations.AddRange(GenerateRecommendations(report));
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Error = ex.Message;
            }

            return report;
        }

        private async Task<AssemblyDocumentationReport> ValidateAssemblyDocumentationAsync(Assembly assembly)
        {
            var report = new AssemblyDocumentationReport
            {
                AssemblyName = assembly.GetName().Name
            };

            var types = assembly.GetTypes().Where(t => t.IsPublic);

            foreach (var type in types)
            {
                var typeReport = await ValidateTypeDocumentationAsync(type);
                report.TypeReports.Add(typeReport);
            }

            // Calculate assembly-level metrics
            report.CalculateMetrics();

            return report;
        }

        private async Task<TypeDocumentationReport> ValidateTypeDocumentationAsync(Type type)
        {
            var report = new TypeDocumentationReport
            {
                TypeName = type.FullName
            };

            // Validate type documentation
            var typeDoc = _xmlParser.GetTypeDocumentation(type);
            report.HasTypeDocumentation = !string.IsNullOrEmpty(typeDoc);

            // Validate members
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName);

            foreach (var member in members)
            {
                var memberReport = ValidateMemberDocumentationAsync(member);
                report.MemberReports.Add(memberReport);
            }

            // Validate interface implementations
            if (type.GetInterfaces().Any())
            {
                report.InterfaceImplementationReports = ValidateInterfaceImplementations(type);
            }

            // Validate code examples
            var examples = await _exampleGenerator.FindTypeExamples(type);
            report.HasCodeExamples = examples.Any();
            report.CodeExampleCount = examples.Count;

            // Check cross-references
            var crossRefs = await _crossRefGenerator.FindTypeReferences(type);
            report.ValidateCrossReferences(crossRefs);

            // Calculate metrics
            report.CalculateMetrics();

            return report;
        }

        private MemberDocumentationReport ValidateMemberDocumentationAsync(MemberInfo member)
        {
            var report = new MemberDocumentationReport
            {
                MemberName = member.Name,
                MemberType = member.MemberType.ToString()
            };

            var xmlDoc = _xmlParser.GetMemberDocumentation(member);
            report.HasDocumentation = xmlDoc != null;

            if (xmlDoc != null)
            {
                report.HasSummary = !string.IsNullOrEmpty(xmlDoc.Summary);
                report.HasRemarks = !string.IsNullOrEmpty(xmlDoc.Remarks);

                if (member is MethodInfo method)
                {
                    report.ValidateMethodDocumentation(method, xmlDoc);
                }
                else if (member is PropertyInfo property)
                {
                    report.ValidatePropertyDocumentation(property, xmlDoc);
                }
            }

            return report;
        }

        private List<InterfaceImplementationReport> ValidateInterfaceImplementations(Type type)
        {
            var reports = new List<InterfaceImplementationReport>();

            foreach (var iface in type.GetInterfaces())
            {
                var report = new InterfaceImplementationReport
                {
                    InterfaceName = iface.FullName,
                    ImplementingType = type.FullName
                };

                var interfaceMap = type.GetInterfaceMap(iface);
                foreach (var methodPair in interfaceMap.InterfaceMethods.Zip(interfaceMap.TargetMethods, (i, t) => new { Interface = i, Target = t }))
                {
                    var methodReport = ValidateInterfaceMethodImplementation(methodPair.Interface, methodPair.Target);
                    report.MethodReports.Add(methodReport);
                }

                report.CalculateCompleteness();
                reports.Add(report);
            }

            return reports;
        }

        private InterfaceMethodReport ValidateInterfaceMethodImplementation(MethodInfo interfaceMethod, MethodInfo implementationMethod)
        {
            var report = new InterfaceMethodReport
            {
                MethodName = interfaceMethod.Name,
                InterfaceDocumentation = _xmlParser.GetMethodDocumentation(interfaceMethod),
                ImplementationDocumentation = _xmlParser.GetMethodDocumentation(implementationMethod)
            };

            report.ValidateDocumentationConsistency();
            return report;
        }

        private List<DocumentationRecommendation> GenerateRecommendations(DocumentationCoverageReport report)
        {
            var recommendations = new List<DocumentationRecommendation>();

            // Add recommendations based on coverage gaps
            if (report.PublicApiCoverage < _config.Thresholds.MinimumPublicApiCoverage)
            {
                recommendations.Add(new DocumentationRecommendation
                {
                    Area = "Public API Coverage",
                    Priority = RecommendationPriority.High,
                    Description = $"Increase public API documentation coverage from {report.PublicApiCoverage:P} to meet minimum threshold of {_config.Thresholds.MinimumPublicApiCoverage:P}",
                    Impact = "Improved developer experience and code maintainability"
                });
            }

            if (report.CodeExampleCoverage < _config.Thresholds.MinimumCodeExampleCoverage)
            {
                recommendations.Add(new DocumentationRecommendation
                {
                    Area = "Code Examples",
                    Priority = RecommendationPriority.Medium,
                    Description = $"Add more code examples to reach minimum coverage of {_config.Thresholds.MinimumCodeExampleCoverage:P}",
                    Impact = "Better understanding of API usage patterns"
                });
            }

            // Add recommendations for interface implementation documentation
            var interfaceIssues = report.AssemblyReports
                .SelectMany(a => a.TypeReports)
                .SelectMany(t => t.InterfaceImplementationReports)
                .Where(i => i.DocumentationCompleteness < _config.Thresholds.MinimumInterfaceDocumentationCompleteness);

            if (interfaceIssues.Any())
            {
                recommendations.Add(new DocumentationRecommendation
                {
                    Area = "Interface Implementations",
                    Priority = RecommendationPriority.Medium,
                    Description = "Improve documentation consistency between interfaces and their implementations",
                    Impact = "Better contract understanding and implementation guidance"
                });
            }

            // Add recommendations for parameter documentation
            if (report.ParameterDocumentationCoverage < _config.Thresholds.MinimumParameterDocumentationCoverage)
            {
                recommendations.Add(new DocumentationRecommendation
                {
                    Area = "Parameter Documentation",
                    Priority = RecommendationPriority.High,
                    Description = "Add missing parameter descriptions in method documentation",
                    Impact = "Clearer understanding of method parameters and proper usage"
                });
            }

            return recommendations;
        }
    }
}