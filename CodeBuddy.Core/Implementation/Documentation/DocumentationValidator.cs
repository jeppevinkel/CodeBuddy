using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Validates documentation quality and coverage
    /// </summary>
    public class DocumentationValidator
    {
        private readonly XmlDocumentationParser _xmlParser;
        private readonly CodeExampleValidator _exampleValidator;
        private readonly CrossReferenceGenerator _crossRefGenerator;
        private readonly XmlDocumentationAnalyzer _xmlAnalyzer;

        public DocumentationValidator(
            XmlDocumentationParser xmlParser,
            CodeExampleValidator exampleValidator,
            CrossReferenceGenerator crossRefGenerator)
        {
            _xmlParser = xmlParser;
            _exampleValidator = exampleValidator;
            _crossRefGenerator = crossRefGenerator;
            _xmlAnalyzer = new XmlDocumentationAnalyzer(xmlParser, this);
        }

        /// <summary>
        /// Performs comprehensive validation of documentation
        /// </summary>
        public async Task<ValidationResult> ValidateAsync(DocumentationSet documentation)
        {
            var result = new ValidationResult
            {
                Issues = new List<DocumentationIssue>()
            };

            // Analyze XML documentation in assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("CodeBuddy")))
            {
                var analysisResult = await _xmlAnalyzer.AnalyzeAssemblyAsync(assembly);
                result.Issues.AddRange(analysisResult.Issues);
                
                // Add analysis results to documentation
                documentation.AnalysisResults ??= new List<DocumentationAnalysisResult>();
                documentation.AnalysisResults.Add(analysisResult);
            }

            // Validate code examples
            await ValidateCodeExamples(documentation, result);

            // Validate cross-references
            await ValidateCrossReferences(documentation, result);

            // Validate markdown content
            await ValidateMarkdownContent(documentation, result);

            // Validate diagrams
            await ValidateDiagrams(documentation, result);

            // Calculate overall documentation quality score
            result.QualityScore = CalculateQualityScore(result.Issues);
            result.Coverage = CalculateOverallCoverage(documentation.AnalysisResults);

            return result;
        }

        /// <summary>
        /// Gets documentation analysis results for a specific assembly
        /// </summary>
        public async Task<DocumentationAnalysisResult> AnalyzeAssemblyAsync(Assembly assembly)
        {
            return await _xmlAnalyzer.AnalyzeAssemblyAsync(assembly);
        }

        /// <summary>
        /// Calculates the overall documentation coverage across all analyzed assemblies
        /// </summary>
        private double CalculateOverallCoverage(List<DocumentationAnalysisResult> analysisResults)
        {
            if (analysisResults == null || !analysisResults.Any())
                return 0.0;

            return analysisResults.Average(r => r.Coverage);
        }

        private async Task ValidateCodeExamples(DocumentationSet docs, ValidationResult result)
        {
            foreach (var example in docs.Examples)
            {
                var exampleValidation = await _exampleValidator.ValidateAsync(example);
                if (!exampleValidation.IsValid)
                {
                    foreach (var error in exampleValidation.Errors)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.InvalidExample,
                            Component = $"Example: {example.Title}",
                            Message = error
                        });
                    }
                }
            }
        }

        private async Task ValidateCrossReferences(DocumentationSet docs, ValidationResult result)
        {
            var references = await _crossRefGenerator.FindAllReferencesAsync(docs);
            foreach (var reference in references)
            {
                if (!reference.IsValid)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.BrokenReference,
                        Component = reference.Source,
                        Message = $"Broken reference to {reference.Target}"
                    });
                }
            }
        }

        private async Task ValidateMarkdownContent(DocumentationSet docs, ValidationResult result)
        {
            foreach (var file in docs.MarkdownFiles)
            {
                // Check for broken links
                var brokenLinks = FindBrokenLinks(file.Content);
                foreach (var link in brokenLinks)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.BrokenLink,
                        Component = file.Path,
                        Message = $"Broken link: {link}"
                    });
                }

                // Check for malformed markdown
                var markdownIssues = ValidateMarkdownSyntax(file.Content);
                result.Issues.AddRange(markdownIssues.Select(issue => new DocumentationIssue
                {
                    Type = IssueType.InvalidMarkdown,
                    Component = file.Path,
                    Message = issue
                }));
            }
        }

        private async Task ValidateDiagrams(DocumentationSet docs, ValidationResult result)
        {
            foreach (var diagram in docs.Diagrams)
            {
                // Validate PlantUML syntax
                if (diagram.Type == DiagramType.PlantUML)
                {
                    var syntaxErrors = ValidatePlantUMLSyntax(diagram.Content);
                    foreach (var error in syntaxErrors)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.InvalidDiagram,
                            Component = diagram.Path,
                            Message = error
                        });
                    }
                }

                // Validate diagram references
                var missingReferences = FindMissingDiagramReferences(diagram, docs);
                foreach (var reference in missingReferences)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.BrokenDiagramReference,
                        Component = diagram.Path,
                        Message = $"Missing reference: {reference}"
                    });
                }
            }
        }

        private List<string> FindBrokenLinks(string content)
        {
            var brokenLinks = new List<string>();
            var linkPattern = @"\[([^\]]+)\]\(([^\)]+)\)";
            var matches = Regex.Matches(content, linkPattern);

            foreach (Match match in matches)
            {
                var link = match.Groups[2].Value;
                if (!IsValidLink(link))
                {
                    brokenLinks.Add(link);
                }
            }

            return brokenLinks;
        }

        private bool IsValidLink(string link)
        {
            // Implement link validation logic
            return true;
        }

        private List<string> ValidateMarkdownSyntax(string content)
        {
            var issues = new List<string>();

            // Check for unclosed code blocks
            var codeBlockCount = Regex.Matches(content, "```").Count;
            if (codeBlockCount % 2 != 0)
            {
                issues.Add("Unclosed code block found");
            }

            // Check for malformed headers
            var headerPattern = @"^#{1,6} .+$";
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#") && !Regex.IsMatch(line, headerPattern))
                {
                    issues.Add($"Malformed header at line {i + 1}: {line}");
                }
            }

            return issues;
        }

        private List<string> ValidatePlantUMLSyntax(string content)
        {
            var issues = new List<string>();

            // Check for basic PlantUML syntax
            if (!content.Contains("@startuml") || !content.Contains("@enduml"))
            {
                issues.Add("Missing @startuml/@enduml tags");
            }

            // Add more PlantUML syntax validation as needed

            return issues;
        }

        private List<string> FindMissingDiagramReferences(Diagram diagram, DocumentationSet docs)
        {
            var missing = new List<string>();

            // Extract all type references from the diagram
            var typePattern = @"class (\w+)|interface (\w+)";
            var matches = Regex.Matches(diagram.Content, typePattern);

            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value ?? match.Groups[2].Value;
                if (!docs.Types.Any(t => t.Name == typeName))
                {
                    missing.Add(typeName);
                }
            }

            return missing;
        }

        private double CalculateQualityScore(List<DocumentationIssue> issues)
        {
            if (!issues.Any())
                return 1.0;

            // Weight different issue types
            var weights = new Dictionary<IssueType, double>
            {
                { IssueType.MissingDescription, 0.5 },
                { IssueType.MissingParameterDescription, 0.3 },
                { IssueType.InvalidExample, 0.7 },
                { IssueType.BrokenReference, 0.8 },
                { IssueType.BrokenLink, 0.6 },
                { IssueType.InvalidMarkdown, 0.4 },
                { IssueType.InvalidDiagram, 0.5 },
                { IssueType.BrokenDiagramReference, 0.6 }
            };

            // Calculate weighted sum of issues
            var totalWeight = issues.Sum(i => weights[i.Type]);
            
            // Normalize to 0-1 range, where 1 is perfect
            return Math.Max(0, 1 - (totalWeight / (issues.Count * 1.0)));
        }
    }
}