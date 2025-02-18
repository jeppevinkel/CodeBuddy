using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    public class TeamDocumentationValidator
    {
        private readonly Dictionary<string, TeamDocumentationConfig> _teamConfigs;
        private readonly DocumentationCoverageAnalyzer _coverageAnalyzer;
        private readonly XmlDocumentationParser _xmlParser;

        public TeamDocumentationValidator(
            Dictionary<string, TeamDocumentationConfig> teamConfigs,
            DocumentationCoverageAnalyzer coverageAnalyzer)
        {
            _teamConfigs = teamConfigs;
            _coverageAnalyzer = coverageAnalyzer;
            _xmlParser = new XmlDocumentationParser();
        }

        public async Task<List<DocumentationIssue>> ValidateTeamDocumentationAsync(
            string teamId, 
            DocumentationCoverageReport coverageReport)
        {
            if (!_teamConfigs.TryGetValue(teamId, out var teamConfig))
                throw new ArgumentException($"No documentation configuration found for team {teamId}");

            var issues = new List<DocumentationIssue>();

            // Filter types managed by the team
            var teamTypes = coverageReport.Types
                .Where(t => teamConfig.ManagedNamespaces.Any(ns => 
                    t.Namespace?.StartsWith(ns, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var type in teamTypes)
            {
                // Check type-specific thresholds
                if (teamConfig.TypeSpecificThresholds.TryGetValue(type.TypeName, out var threshold))
                {
                    if (type.TypeCoverage < threshold)
                    {
                        issues.Add(new DocumentationIssue
                        {
                            Component = $"{type.Namespace}.{type.TypeName}",
                            IssueType = "TypeSpecificThresholdNotMet",
                            Description = $"Type coverage {type.TypeCoverage:F1}% is below the required threshold of {threshold}%",
                            Severity = IssueSeverity.Error
                        });
                    }
                }

                // Validate documentation style rules
                await ValidateStyleRules(type, teamConfig.StyleRules, issues);

                // Check required sections
                foreach (var section in teamConfig.RequiredSections)
                {
                    if (!HasRequiredSection(type, section))
                    {
                        issues.Add(new DocumentationIssue
                        {
                            Component = $"{type.Namespace}.{type.TypeName}",
                            IssueType = "MissingRequiredSection",
                            Description = $"Required documentation section '{section}' is missing",
                            Severity = IssueSeverity.Warning
                        });
                    }
                }
            }

            return issues;
        }

        private async Task ValidateStyleRules(TypeCoverageInfo type, StyleValidationRules rules, List<DocumentationIssue> issues)
        {
            var docs = await _xmlParser.GetTypeDocumentationAsync(Type.GetType($"{type.Namespace}.{type.TypeName}"));
            if (docs == null) return;

            // Validate summary length
            if (rules.MinWordCountInSummary > 0)
            {
                var wordCount = docs.Summary?.Split(new[] { ' ', '\t', '\n', '\r' }, 
                    StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
                if (wordCount < rules.MinWordCountInSummary)
                {
                    issues.Add(new DocumentationIssue
                    {
                        Component = $"{type.Namespace}.{type.TypeName}",
                        IssueType = "InsufficientSummaryLength",
                        Description = $"Summary has {wordCount} words, minimum required is {rules.MinWordCountInSummary}",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Check for prohibited terms
            foreach (var term in rules.ProhibitedTerms)
            {
                if (docs.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    issues.Add(new DocumentationIssue
                    {
                        Component = $"{type.Namespace}.{type.TypeName}",
                        IssueType = "ProhibitedTermUsed",
                        Description = $"Documentation contains prohibited term '{term}'",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Validate method documentation verbs
            if (rules.RequireVerbsInMethodDocs)
            {
                foreach (var method in type.MissingDocumentation.Where(m => m.MemberType == "Method"))
                {
                    var methodDocs = await _xmlParser.GetMethodDocumentationAsync(
                        Type.GetType($"{type.Namespace}.{type.TypeName}")
                            .GetMethod(method.MemberName));

                    if (methodDocs?.Summary != null && !StartsWithVerb(methodDocs.Summary))
                    {
                        issues.Add(new DocumentationIssue
                        {
                            Component = $"{type.Namespace}.{type.TypeName}.{method.MemberName}",
                            IssueType = "MethodDocsMissingVerb",
                            Description = "Method documentation should start with a verb",
                            Severity = IssueSeverity.Warning
                        });
                    }
                }
            }

            // Check for required terms in class documentation
            if (rules.RequiredTermsInClassDocs.Any())
            {
                var missingTerms = rules.RequiredTermsInClassDocs
                    .Where(term => !docs.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? true)
                    .ToList();

                if (missingTerms.Any())
                {
                    issues.Add(new DocumentationIssue
                    {
                        Component = $"{type.Namespace}.{type.TypeName}",
                        IssueType = "MissingRequiredTerms",
                        Description = $"Class documentation is missing required terms: {string.Join(", ", missingTerms)}",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Validate full sentences
            if (rules.RequireFullSentences && !string.IsNullOrEmpty(docs.Summary))
            {
                if (!EndsWithSentenceTerminator(docs.Summary))
                {
                    issues.Add(new DocumentationIssue
                    {
                        Component = $"{type.Namespace}.{type.TypeName}",
                        IssueType = "IncompleteStatement",
                        Description = "Documentation summary should end with proper punctuation",
                        Severity = IssueSeverity.Warning
                    });
                }
            }
        }

        private bool StartsWithVerb(string text)
        {
            // Basic verb detection - could be enhanced with a proper NLP library
            var firstWord = text.Split(' ').First().ToLower();
            return firstWord.EndsWith("s") || firstWord.EndsWith("es") || 
                   firstWord.EndsWith("ed") || firstWord.EndsWith("ing");
        }

        private bool EndsWithSentenceTerminator(string text)
        {
            return text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?");
        }

        private bool HasRequiredSection(TypeCoverageInfo type, string section)
        {
            // Implementation would check for specific documentation sections
            // This is a placeholder for the actual implementation
            return true;
        }
    }
}