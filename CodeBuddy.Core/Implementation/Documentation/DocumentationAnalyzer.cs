using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Analyzes documentation coverage and generates recommendations
    /// </summary>
    public class DocumentationAnalyzer
    {
        private const double COVERAGE_THRESHOLD = 0.8; // 80% coverage threshold

        /// <summary>
        /// Analyzes documentation coverage for the given assemblies
        /// </summary>
        public async Task<DocumentationCoverageResult> AnalyzeCoverage(IEnumerable<Assembly> assemblies)
        {
            var result = new DocumentationCoverageResult();
            var gaps = new List<DocumentationGap>();
            var stats = new DocumentationCoverageStats();
            var totalTypes = 0;
            var documentedTypes = 0;
            var publicApiCount = 0;
            var documentedApiCount = 0;

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.IsPublic);
                foreach (var type in types)
                {
                    totalTypes++;
                    var hasTypeDoc = HasDocumentation(type);
                    if (hasTypeDoc) documentedTypes++;

                    // Check public members
                    var publicMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => !m.IsSpecialName);

                    foreach (var member in publicMembers)
                    {
                        publicApiCount++;
                        if (HasDocumentation(member)) documentedApiCount++;
                        else
                        {
                            gaps.Add(new DocumentationGap
                            {
                                Component = type.FullName,
                                MissingElement = member.Name,
                                Impact = "Missing API documentation reduces code maintainability",
                                Recommendation = $"Add XML documentation for {member.Name}"
                            });
                        }
                    }

                    // Track namespace coverage
                    var ns = type.Namespace ?? "Global";
                    if (!stats.CoverageByNamespace.ContainsKey(ns))
                    {
                        stats.CoverageByNamespace[ns] = 0;
                    }
                    stats.CoverageByNamespace[ns] += hasTypeDoc ? 1 : 0;
                }
            }

            // Calculate coverage metrics
            stats.TotalComponents = totalTypes;
            stats.DocumentedComponents = documentedTypes;
            stats.OverallCoverage = documentedTypes / (double)totalTypes;
            stats.PublicApiCoverage = documentedApiCount / (double)publicApiCount;

            // Normalize namespace coverage
            foreach (var ns in stats.CoverageByNamespace.Keys.ToList())
            {
                var nsTypes = assemblies.SelectMany(a => a.GetTypes())
                    .Count(t => t.Namespace == ns);
                stats.CoverageByNamespace[ns] /= nsTypes;
            }

            result.Coverage = stats;
            result.Gaps = gaps;

            // Generate recommendations
            if (stats.OverallCoverage < COVERAGE_THRESHOLD)
            {
                result.Recommendations.Add($"Overall documentation coverage ({stats.OverallCoverage:P}) is below the recommended threshold of {COVERAGE_THRESHOLD:P}");
            }

            var lowCoverageNamespaces = stats.CoverageByNamespace
                .Where(kvp => kvp.Value < COVERAGE_THRESHOLD)
                .Select(kvp => kvp.Key);

            foreach (var ns in lowCoverageNamespaces)
            {
                result.Recommendations.Add($"Namespace {ns} has low documentation coverage ({stats.CoverageByNamespace[ns]:P})");
            }

            return result;
        }

        /// <summary>
        /// Generates recommendations based on documentation issues
        /// </summary>
        public List<string> GenerateRecommendations(IEnumerable<DocumentationIssue> issues)
        {
            var recommendations = new List<string>();
            var criticalIssues = issues.Where(i => i.Severity == IssueSeverity.Critical);
            var errorIssues = issues.Where(i => i.Severity == IssueSeverity.Error);
            var warningIssues = issues.Where(i => i.Severity == IssueSeverity.Warning);

            if (criticalIssues.Any())
            {
                recommendations.Add("Critical documentation issues found that must be addressed:");
                recommendations.AddRange(criticalIssues.Select(i => $"- {i.Component}: {i.Description}"));
            }

            var commonErrors = errorIssues
                .GroupBy(i => i.IssueType)
                .OrderByDescending(g => g.Count());

            foreach (var error in commonErrors)
            {
                recommendations.Add($"Found {error.Count()} {error.Key} issues that should be fixed");
            }

            if (warningIssues.Any())
            {
                var warningTypes = warningIssues
                    .GroupBy(i => i.IssueType)
                    .OrderByDescending(g => g.Count());

                foreach (var warning in warningTypes)
                {
                    recommendations.Add($"Consider addressing {warning.Count()} {warning.Key} warnings");
                }
            }

            return recommendations;
        }

        /// <summary>
        /// Generates resource management best practices
        /// </summary>
        public List<ResourceBestPractice> GenerateResourceBestPractices()
        {
            return new List<ResourceBestPractice>
            {
                new ResourceBestPractice
                {
                    Title = "Use IDisposable Pattern",
                    Description = "Implement IDisposable for classes that manage unmanaged resources",
                    Rationale = "Ensures proper resource cleanup and prevents memory leaks",
                    Guidelines = new List<string>
                    {
                        "Implement IDisposable interface",
                        "Create protected virtual Dispose(bool) method",
                        "Call Dispose(true) in Dispose() method",
                        "Call GC.SuppressFinalize(this) in Dispose()",
                        "Implement finalizer if needed"
                    }
                },
                new ResourceBestPractice
                {
                    Title = "Resource Pre-allocation",
                    Description = "Pre-allocate resources when possible to improve performance",
                    Rationale = "Reduces runtime allocations and improves performance",
                    Guidelines = new List<string>
                    {
                        "Use object pools for frequently created objects",
                        "Pre-allocate buffers for known sizes",
                        "Cache frequently used resources",
                        "Monitor resource usage patterns"
                    }
                },
                new ResourceBestPractice
                {
                    Title = "Async Resource Management",
                    Description = "Use async/await for resource operations",
                    Rationale = "Improves application responsiveness and resource utilization",
                    Guidelines = new List<string>
                    {
                        "Use async methods for I/O operations",
                        "Implement IAsyncDisposable when needed",
                        "Handle exceptions in async cleanup",
                        "Use CancellationToken for cancellable operations"
                    }
                }
            };
        }

        private bool HasDocumentation(MemberInfo member)
        {
            return member.GetCustomAttributes()
                .Any(a => a.GetType().Name.Contains("Description") || a.GetType().Name.Contains("Documentation"));
        }
    }
}