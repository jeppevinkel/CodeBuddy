using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Analyzes and validates XML documentation comments in C# source code
    /// </summary>
    public class XmlDocumentationAnalyzer
    {
        private readonly XmlDocumentationParser _xmlParser;
        private readonly DocumentationValidator _validator;

        public XmlDocumentationAnalyzer(
            XmlDocumentationParser xmlParser,
            DocumentationValidator validator)
        {
            _xmlParser = xmlParser;
            _validator = validator;
        }

        /// <summary>
        /// Analyzes XML documentation for a given assembly
        /// </summary>
        /// <param name="assembly">The assembly to analyze</param>
        /// <returns>Documentation analysis results</returns>
        public async Task<DocumentationAnalysisResult> AnalyzeAssemblyAsync(Assembly assembly)
        {
            var result = new DocumentationAnalysisResult
            {
                AssemblyName = assembly.GetName().Name,
                TypeResults = new List<TypeDocumentationResult>(),
                Issues = new List<DocumentationIssue>()
            };

            var types = assembly.GetTypes()
                .Where(t => t.IsPublic);

            foreach (var type in types)
            {
                var typeResult = await AnalyzeTypeAsync(type);
                result.TypeResults.Add(typeResult);
                result.Issues.AddRange(typeResult.Issues);
            }

            result.Coverage = CalculateCoverage(result.TypeResults);
            result.QualityScore = CalculateQualityScore(result.Issues);

            return result;
        }

        /// <summary>
        /// Analyzes XML documentation for a specific type
        /// </summary>
        private async Task<TypeDocumentationResult> AnalyzeTypeAsync(Type type)
        {
            var result = new TypeDocumentationResult
            {
                TypeName = type.FullName,
                Issues = new List<DocumentationIssue>(),
                MemberResults = new List<MemberDocumentationResult>()
            };

            // Analyze type documentation
            var typeDoc = _xmlParser.GetTypeDocumentation(type);
            if (string.IsNullOrWhiteSpace(typeDoc))
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingDescription,
                    Component = type.Name,
                    Message = "Type is missing XML documentation",
                    Severity = IssueSeverity.Warning
                });
            }
            else
            {
                // Validate type documentation quality
                result.Issues.AddRange(ValidateDocumentationQuality(typeDoc, type.Name));
            }

            // Analyze methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName);

            foreach (var method in methods)
            {
                var methodResult = AnalyzeMethodDocumentation(method);
                result.MemberResults.Add(methodResult);
                result.Issues.AddRange(methodResult.Issues);
            }

            // Analyze properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var property in properties)
            {
                var propertyResult = AnalyzePropertyDocumentation(property);
                result.MemberResults.Add(propertyResult);
                result.Issues.AddRange(propertyResult.Issues);
            }

            return result;
        }

        /// <summary>
        /// Analyzes XML documentation for a method
        /// </summary>
        private MemberDocumentationResult AnalyzeMethodDocumentation(MethodInfo method)
        {
            var result = new MemberDocumentationResult
            {
                MemberName = method.Name,
                MemberType = "Method",
                Issues = new List<DocumentationIssue>()
            };

            var methodDoc = _xmlParser.GetMethodDocumentation(method);
            if (methodDoc == null)
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingDescription,
                    Component = $"{method.DeclaringType.Name}.{method.Name}",
                    Message = "Method is missing XML documentation",
                    Severity = IssueSeverity.Warning
                });
                return result;
            }

            // Check for missing parameter documentation
            foreach (var parameter in method.GetParameters())
            {
                var paramDoc = methodDoc.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
                if (paramDoc == null || string.IsNullOrWhiteSpace(paramDoc.Description))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingParameterDescription,
                        Component = $"{method.DeclaringType.Name}.{method.Name}",
                        Message = $"Parameter '{parameter.Name}' is missing description",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Check return value documentation
            if (method.ReturnType != typeof(void) && 
                (methodDoc.Returns == null || string.IsNullOrWhiteSpace(methodDoc.Returns.Description)))
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingReturnDescription,
                    Component = $"{method.DeclaringType.Name}.{method.Name}",
                    Message = "Missing return value documentation",
                    Severity = IssueSeverity.Warning
                });
            }

            // Check exception documentation
            var thrownExceptions = FindThrownExceptions(method);
            foreach (var exception in thrownExceptions)
            {
                if (!methodDoc.Exceptions.Any(e => e.Type == exception))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingExceptionDescription,
                        Component = $"{method.DeclaringType.Name}.{method.Name}",
                        Message = $"Missing documentation for thrown exception '{exception}'",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Validate quality of existing documentation
            result.Issues.AddRange(ValidateDocumentationQuality(methodDoc.Summary, 
                $"{method.DeclaringType.Name}.{method.Name}"));

            return result;
        }

        /// <summary>
        /// Analyzes XML documentation for a property
        /// </summary>
        private MemberDocumentationResult AnalyzePropertyDocumentation(PropertyInfo property)
        {
            var result = new MemberDocumentationResult
            {
                MemberName = property.Name,
                MemberType = "Property",
                Issues = new List<DocumentationIssue>()
            };

            var propertyDoc = _xmlParser.GetPropertyDocumentation(property);
            if (string.IsNullOrWhiteSpace(propertyDoc))
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingDescription,
                    Component = $"{property.DeclaringType.Name}.{property.Name}",
                    Message = "Property is missing XML documentation",
                    Severity = IssueSeverity.Warning
                });
            }
            else
            {
                // Validate property documentation quality
                result.Issues.AddRange(ValidateDocumentationQuality(propertyDoc, 
                    $"{property.DeclaringType.Name}.{property.Name}"));
            }

            return result;
        }

        /// <summary>
        /// Validates the quality of documentation text
        /// </summary>
        private IEnumerable<DocumentationIssue> ValidateDocumentationQuality(string documentation, string component)
        {
            var issues = new List<DocumentationIssue>();

            if (!string.IsNullOrWhiteSpace(documentation))
            {
                // Check for very short descriptions
                if (documentation.Length < 10)
                {
                    issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.LowQualityDescription,
                        Component = component,
                        Message = "Documentation description is too short",
                        Severity = IssueSeverity.Info
                    });
                }

                // Check for common placeholder text
                var placeholderPatterns = new[]
                {
                    "TODO",
                    "to be implemented",
                    "to be added",
                    "fill this in",
                    "needs description"
                };

                if (placeholderPatterns.Any(p => documentation.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.PlaceholderDocumentation,
                        Component = component,
                        Message = "Documentation contains placeholder text",
                        Severity = IssueSeverity.Warning
                    });
                }

                // Check for broken code references
                var codeRefs = Regex.Matches(documentation, "<see cref=\"([^\"]+)\"/>");
                foreach (Match match in codeRefs)
                {
                    var reference = match.Groups[1].Value;
                    if (!ValidateCodeReference(reference))
                    {
                        issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.BrokenReference,
                            Component = component,
                            Message = $"Documentation contains broken code reference: {reference}",
                            Severity = IssueSeverity.Warning
                        });
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Finds exceptions that can be thrown by a method
        /// </summary>
        private IEnumerable<string> FindThrownExceptions(MethodInfo method)
        {
            // This is a simplified implementation. In practice, you would:
            // 1. Analyze the method body for throw statements
            // 2. Check calls to other methods that can throw
            // 3. Check custom exception attributes
            return new string[] { }; // Placeholder
        }

        /// <summary>
        /// Validates a code reference in documentation
        /// </summary>
        private bool ValidateCodeReference(string reference)
        {
            // This is a simplified implementation. In practice, you would:
            // 1. Parse the reference format
            // 2. Look up the referenced member in loaded assemblies
            // 3. Verify the reference is valid
            return true; // Placeholder
        }

        /// <summary>
        /// Calculates documentation coverage percentage
        /// </summary>
        private double CalculateCoverage(List<TypeDocumentationResult> results)
        {
            var totalMembers = 0;
            var documentedMembers = 0;

            foreach (var result in results)
            {
                totalMembers++; // Count the type itself
                documentedMembers += result.Issues.Any(i => i.Type == IssueType.MissingDescription) ? 0 : 1;

                foreach (var memberResult in result.MemberResults)
                {
                    totalMembers++;
                    documentedMembers += memberResult.Issues.Any(i => i.Type == IssueType.MissingDescription) ? 0 : 1;
                }
            }

            return totalMembers == 0 ? 1.0 : (double)documentedMembers / totalMembers;
        }

        /// <summary>
        /// Calculates overall documentation quality score
        /// </summary>
        private double CalculateQualityScore(List<DocumentationIssue> issues)
        {
            if (!issues.Any())
                return 1.0;

            // Weight different issue types
            var weights = new Dictionary<IssueType, double>
            {
                { IssueType.MissingDescription, 0.5 },
                { IssueType.MissingParameterDescription, 0.3 },
                { IssueType.MissingReturnDescription, 0.3 },
                { IssueType.MissingExceptionDescription, 0.3 },
                { IssueType.LowQualityDescription, 0.2 },
                { IssueType.PlaceholderDocumentation, 0.4 },
                { IssueType.BrokenReference, 0.4 }
            };

            var totalWeight = issues.Sum(i => weights[i.Type]);
            return Math.Max(0, 1 - (totalWeight / (issues.Count * 1.0)));
        }
    }
}