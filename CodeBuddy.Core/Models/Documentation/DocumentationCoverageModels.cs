using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Configuration for documentation coverage validation
    /// </summary>
    public class DocumentationCoverageConfig
    {
        /// <summary>
        /// Coverage threshold requirements
        /// </summary>
        public DocumentationCoverageThresholds Thresholds { get; set; } = new();

        /// <summary>
        /// Optional list of assemblies to include in coverage analysis
        /// </summary>
        public List<string> IncludedAssemblies { get; set; } = new();

        /// <summary>
        /// Optional list of assemblies to exclude from coverage analysis
        /// </summary>
        public List<string> ExcludedAssemblies { get; set; } = new();
    }

    /// <summary>
    /// Defines minimum coverage thresholds for documentation validation
    /// </summary>
    public class DocumentationCoverageThresholds
    {
        /// <summary>
        /// Minimum required coverage for public APIs (0.0 to 1.0)
        /// </summary>
        public double MinimumPublicApiCoverage { get; set; } = 0.8;

        /// <summary>
        /// Minimum required coverage for XML parameter documentation (0.0 to 1.0)
        /// </summary>
        public double MinimumParameterDocumentationCoverage { get; set; } = 0.9;

        /// <summary>
        /// Minimum required code example coverage (0.0 to 1.0)
        /// </summary>
        public double MinimumCodeExampleCoverage { get; set; } = 0.5;

        /// <summary>
        /// Minimum completeness score for interface implementation documentation (0.0 to 1.0)
        /// </summary>
        public double MinimumInterfaceDocumentationCompleteness { get; set; } = 0.8;

        /// <summary>
        /// Minimum required cross-reference validity (0.0 to 1.0)
        /// </summary>
        public double MinimumCrossReferenceValidity { get; set; } = 0.95;
    }

    /// <summary>
    /// Comprehensive report of documentation coverage analysis
    /// </summary>
    public class DocumentationCoverageReport
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<AssemblyDocumentationReport> AssemblyReports { get; set; } = new();
        public List<DocumentationRecommendation> Recommendations { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        // Overall metrics
        public double PublicApiCoverage { get; set; }
        public double ParameterDocumentationCoverage { get; set; }
        public double CodeExampleCoverage { get; set; }
        public double InterfaceImplementationCoverage { get; set; }
        public double CrossReferenceValidity { get; set; }

        public void CalculateOverallMetrics()
        {
            if (!AssemblyReports.Any()) return;

            PublicApiCoverage = AssemblyReports.Average(a => a.PublicApiCoverage);
            ParameterDocumentationCoverage = AssemblyReports.Average(a => a.ParameterDocumentationCoverage);
            CodeExampleCoverage = AssemblyReports.Average(a => a.CodeExampleCoverage);
            InterfaceImplementationCoverage = AssemblyReports.Average(a => a.InterfaceImplementationCoverage);
            CrossReferenceValidity = AssemblyReports.Average(a => a.CrossReferenceValidity);
        }

        public void ValidateThresholds(DocumentationCoverageThresholds thresholds)
        {
            if (PublicApiCoverage < thresholds.MinimumPublicApiCoverage)
            {
                Warnings.Add($"Public API documentation coverage ({PublicApiCoverage:P}) is below minimum threshold ({thresholds.MinimumPublicApiCoverage:P})");
            }

            if (ParameterDocumentationCoverage < thresholds.MinimumParameterDocumentationCoverage)
            {
                Warnings.Add($"Parameter documentation coverage ({ParameterDocumentationCoverage:P}) is below minimum threshold ({thresholds.MinimumParameterDocumentationCoverage:P})");
            }

            if (CodeExampleCoverage < thresholds.MinimumCodeExampleCoverage)
            {
                Warnings.Add($"Code example coverage ({CodeExampleCoverage:P}) is below minimum threshold ({thresholds.MinimumCodeExampleCoverage:P})");
            }

            if (InterfaceImplementationCoverage < thresholds.MinimumInterfaceDocumentationCompleteness)
            {
                Warnings.Add($"Interface implementation documentation coverage ({InterfaceImplementationCoverage:P}) is below minimum threshold ({thresholds.MinimumInterfaceDocumentationCompleteness:P})");
            }

            if (CrossReferenceValidity < thresholds.MinimumCrossReferenceValidity)
            {
                Warnings.Add($"Cross-reference validity ({CrossReferenceValidity:P}) is below minimum threshold ({thresholds.MinimumCrossReferenceValidity:P})");
            }
        }
    }

    /// <summary>
    /// Documentation coverage report for a specific assembly
    /// </summary>
    public class AssemblyDocumentationReport
    {
        public string AssemblyName { get; set; }
        public List<TypeDocumentationReport> TypeReports { get; set; } = new();
        
        public double PublicApiCoverage { get; set; }
        public double ParameterDocumentationCoverage { get; set; }
        public double CodeExampleCoverage { get; set; }
        public double InterfaceImplementationCoverage { get; set; }
        public double CrossReferenceValidity { get; set; }

        public void CalculateMetrics()
        {
            if (!TypeReports.Any()) return;

            PublicApiCoverage = TypeReports.Average(t => t.DocumentationCoverage);
            ParameterDocumentationCoverage = TypeReports.Average(t => t.ParameterDocumentationCoverage);
            CodeExampleCoverage = TypeReports.Average(t => t.CodeExampleCoverage);
            InterfaceImplementationCoverage = TypeReports
                .Where(t => t.InterfaceImplementationReports.Any())
                .Average(t => t.InterfaceImplementationReports.Average(i => i.DocumentationCompleteness));
            CrossReferenceValidity = TypeReports.Average(t => t.CrossReferenceValidity);
        }
    }

    /// <summary>
    /// Documentation coverage report for a specific type
    /// </summary>
    public class TypeDocumentationReport
    {
        public string TypeName { get; set; }
        public bool HasTypeDocumentation { get; set; }
        public List<MemberDocumentationReport> MemberReports { get; set; } = new();
        public List<InterfaceImplementationReport> InterfaceImplementationReports { get; set; } = new();
        public bool HasCodeExamples { get; set; }
        public int CodeExampleCount { get; set; }
        
        public double DocumentationCoverage { get; set; }
        public double ParameterDocumentationCoverage { get; set; }
        public double CodeExampleCoverage { get; set; }
        public double CrossReferenceValidity { get; set; }

        public void CalculateMetrics()
        {
            if (!MemberReports.Any()) return;

            var totalMembers = MemberReports.Count + 1; // +1 for type itself
            var documentedMembers = (HasTypeDocumentation ? 1 : 0) + MemberReports.Count(m => m.HasDocumentation);
            DocumentationCoverage = (double)documentedMembers / totalMembers;

            var methodReports = MemberReports.Where(m => m.MemberType == "Method").ToList();
            if (methodReports.Any())
            {
                ParameterDocumentationCoverage = methodReports.Average(m => m.ParameterDocumentationCoverage);
            }

            CodeExampleCoverage = HasCodeExamples ? 1.0 : 0.0;
        }

        public void ValidateCrossReferences(List<CrossReference> references)
        {
            if (!references.Any())
            {
                CrossReferenceValidity = 1.0;
                return;
            }

            var validRefs = references.Count(r => r.IsValid);
            CrossReferenceValidity = (double)validRefs / references.Count;
        }
    }

    /// <summary>
    /// Documentation coverage report for a specific member
    /// </summary>
    public class MemberDocumentationReport
    {
        public string MemberName { get; set; }
        public string MemberType { get; set; }
        public bool HasDocumentation { get; set; }
        public bool HasSummary { get; set; }
        public bool HasRemarks { get; set; }
        public double ParameterDocumentationCoverage { get; set; }

        public void ValidateMethodDocumentation(System.Reflection.MethodInfo method, XmlDocumentation xmlDoc)
        {
            var parameters = method.GetParameters();
            if (!parameters.Any())
            {
                ParameterDocumentationCoverage = 1.0;
                return;
            }

            var documentedParams = parameters.Count(p => 
                xmlDoc.Parameters.Any(xp => xp.Name == p.Name && !string.IsNullOrEmpty(xp.Description)));
            ParameterDocumentationCoverage = (double)documentedParams / parameters.Length;
        }

        public void ValidatePropertyDocumentation(System.Reflection.PropertyInfo property, XmlDocumentation xmlDoc)
        {
            // Properties don't have parameters, so coverage is based on summary presence
            ParameterDocumentationCoverage = HasSummary ? 1.0 : 0.0;
        }
    }

    /// <summary>
    /// Report on interface implementation documentation consistency
    /// </summary>
    public class InterfaceImplementationReport
    {
        public string InterfaceName { get; set; }
        public string ImplementingType { get; set; }
        public List<InterfaceMethodReport> MethodReports { get; set; } = new();
        public double DocumentationCompleteness { get; set; }

        public void CalculateCompleteness()
        {
            if (!MethodReports.Any()) return;

            var totalMethods = MethodReports.Count;
            var completelyDocumented = MethodReports.Count(m => m.IsDocumentationConsistent);
            DocumentationCompleteness = (double)completelyDocumented / totalMethods;
        }
    }

    /// <summary>
    /// Report on interface method implementation documentation
    /// </summary>
    public class InterfaceMethodReport
    {
        public string MethodName { get; set; }
        public XmlDocumentation InterfaceDocumentation { get; set; }
        public XmlDocumentation ImplementationDocumentation { get; set; }
        public bool IsDocumentationConsistent { get; set; }

        public void ValidateDocumentationConsistency()
        {
            if (InterfaceDocumentation == null || ImplementationDocumentation == null)
            {
                IsDocumentationConsistent = false;
                return;
            }

            // Check summary consistency
            var summaryConsistent = !string.IsNullOrEmpty(InterfaceDocumentation.Summary) &&
                                  !string.IsNullOrEmpty(ImplementationDocumentation.Summary);

            // Check parameter documentation consistency
            var paramConsistent = InterfaceDocumentation.Parameters.All(ip =>
                ImplementationDocumentation.Parameters.Any(imp =>
                    imp.Name == ip.Name && !string.IsNullOrEmpty(imp.Description)));

            IsDocumentationConsistent = summaryConsistent && paramConsistent;
        }
    }

    /// <summary>
    /// Recommendation for improving documentation coverage
    /// </summary>
    public class DocumentationRecommendation
    {
        public string Area { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
    }

    /// <summary>
    /// Priority level for documentation recommendations
    /// </summary>
    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}