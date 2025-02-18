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
    /// Validates documentation against defined standards and guidelines
    /// </summary>
    public class DocumentationValidator
    {
        private readonly XmlDocumentationParser _xmlParser;
        private readonly CodeExampleValidator _exampleValidator;
        private readonly CrossReferenceGenerator _crossRefGenerator;
        private readonly DocumentationStandard _standard;
        private readonly UsageExampleGenerator _exampleGenerator;
        private readonly DocumentationVersionManager _versionManager;

        public DocumentationValidator(
            XmlDocumentationParser xmlParser,
            CodeExampleValidator exampleValidator,
            CrossReferenceGenerator crossRefGenerator,
            DocumentationStandard standard,
            UsageExampleGenerator exampleGenerator,
            DocumentationVersionManager versionManager)
        {
            _xmlParser = xmlParser;
            _exampleValidator = exampleValidator;
            _crossRefGenerator = crossRefGenerator;
            _standard = standard;
            _exampleGenerator = exampleGenerator;
            _versionManager = versionManager;
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

            // Validate API documentation coverage
            await ValidateApiDocumentationCoverage(documentation, result);

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

            return result;
        }

        /// <summary>
        /// Validates the provided documentation against the defined standards
        /// </summary>
        public async Task<ValidationResult> ValidateAgainstStandardsAsync(DocumentationSet documentation)
        {
            var result = new ValidationResult
            {
                Issues = new List<DocumentationIssue>()
            };

            // Validate against style guide
            await ValidateStyleGuide(documentation, result);

            // Validate API documentation coverage
            await ValidateApiDocumentationCoverage(documentation, result);

            // Validate code examples
            await ValidateCodeExamples(documentation, result);

            // Validate cross-references
            await ValidateCrossReferences(documentation, result);

            // Validate versioning
            await ValidateVersioning(documentation, result);

            // Validate sections and requirements
            await ValidateDocumentationRequirements(documentation, result);

            // Validate custom rules
            await ValidateCustomRules(documentation, result);

            // Calculate quality score
            result.QualityScore = CalculateQualityScore(result.Issues);

            return result;
        }

        private async Task ValidateStyleGuide(DocumentationSet docs, ValidationResult result)
        {
            foreach (var file in docs.MarkdownFiles)
            {
                // Validate markdown rules
                await ValidateMarkdownRules(file, result);

                // Validate prohibited terms
                await ValidateTerms(file, result);

                // Validate links
                await ValidateLinks(file, result);
            }
        }

        private async Task ValidateMarkdownRules(MarkdownFile file, ValidationResult result)
        {
            var rules = _standard.StyleGuide.MarkdownRules;

            // Check header depth
            var headers = Regex.Matches(file.Content, @"^#{1,6} .+$", RegexOptions.Multiline);
            foreach (Match header in headers)
            {
                var depth = header.Value.TakeWhile(c => c == '#').Count();
                if (depth > rules.MaxHeaderDepth)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.StyleViolation,
                        Component = file.Path,
                        Message = $"Header depth {depth} exceeds maximum allowed depth of {rules.MaxHeaderDepth}",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Check line length
            var lines = file.Content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > rules.MaxLineLength)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.StyleViolation,
                        Component = file.Path,
                        Message = $"Line {i + 1} exceeds maximum length of {rules.MaxLineLength} characters",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Validate front matter
            if (rules.RequiredFrontMatter.Any())
            {
                var frontMatter = ExtractFrontMatter(file.Content);
                var missingFields = rules.RequiredFrontMatter.Except(frontMatter.Keys);
                foreach (var field in missingFields)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingMetadata,
                        Component = file.Path,
                        Message = $"Missing required front matter field: {field}",
                        Severity = IssueSeverity.Error
                    });
                }
            }
        }

        private async Task ValidateTerms(MarkdownFile file, ValidationResult result)
        {
            var guide = _standard.StyleGuide;

            // Check for prohibited terms
            foreach (var term in guide.ProhibitedTerms)
            {
                var matches = Regex.Matches(file.Content, term, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.ProhibitedTerm,
                        Component = file.Path,
                        Message = $"Found prohibited term: {term}",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Check for inconsistent term usage
            foreach (var replacement in guide.TermReplacements)
            {
                var matches = Regex.Matches(file.Content, replacement.Key, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.InconsistentTerminology,
                        Component = file.Path,
                        Message = $"Use '{replacement.Value}' instead of '{replacement.Key}'",
                        Severity = IssueSeverity.Warning
                    });
                }
            }
        }

        private async Task ValidateLinks(MarkdownFile file, ValidationResult result)
        {
            foreach (var rule in _standard.StyleGuide.LinkRules)
            {
                var links = ExtractLinks(file.Content);
                foreach (var link in links)
                {
                    if (rule.RequireHttps && link.StartsWith("http://"))
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.InsecureLink,
                            Component = file.Path,
                            Message = $"Link must use HTTPS: {link}",
                            Severity = IssueSeverity.Error
                        });
                    }

                    if (!rule.AllowExternal && IsExternalLink(link))
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.ExternalLink,
                            Component = file.Path,
                            Message = $"External links are not allowed: {link}",
                            Severity = IssueSeverity.Error
                        });
                    }

                    if (rule.AllowedDomains.Any() && !rule.AllowedDomains.Any(d => link.Contains(d)))
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.DisallowedDomain,
                            Component = file.Path,
                            Message = $"Link domain not in allowed list: {link}",
                            Severity = IssueSeverity.Error
                        });
                    }
                }
            }
        }

        private async Task ValidateApiDocumentationCoverage(DocumentationSet docs, ValidationResult result)
        {
            foreach (var type in docs.Types)
            {
                // Check type documentation
                if (string.IsNullOrWhiteSpace(type.Description))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingDescription,
                        Component = $"Type: {type.Name}",
                        Message = "Type is missing description"
                    });
                }

                // Check method documentation
                foreach (var method in type.Methods)
                {
                    if (string.IsNullOrWhiteSpace(method.Description))
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.MissingDescription,
                            Component = $"Method: {type.Name}.{method.Name}",
                            Message = "Method is missing description"
                        });
                    }

                    // Check parameter documentation
                    foreach (var param in method.Parameters)
                    {
                        if (string.IsNullOrWhiteSpace(param.Description))
                        {
                            result.Issues.Add(new DocumentationIssue
                            {
                                Type = IssueType.MissingParameterDescription,
                                Component = $"Parameter: {type.Name}.{method.Name}({param.Name})",
                                Message = "Parameter is missing description"
                            });
                        }
                    }
                }
            }
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

        private async Task ValidateDocumentationRequirements(DocumentationSet docs, ValidationResult result)
        {
            foreach (var file in docs.MarkdownFiles)
            {
                var docType = DetermineDocumentationType(file);
                if (!_standard.Requirements.TryGetValue(docType.ToString(), out var requirements))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.UnknownDocumentationType,
                        Component = file.Path,
                        Message = $"Unknown documentation type: {docType}",
                        Severity = IssueSeverity.Error
                    });
                    continue;
                }

                // Validate required sections
                var sections = ExtractSections(file.Content);
                var missingSections = requirements.RequiredSections.Except(sections.Keys);
                foreach (var section in missingSections)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingSection,
                        Component = file.Path,
                        Message = $"Missing required section: {section}",
                        Severity = IssueSeverity.Error
                    });
                }

                // Validate section requirements
                foreach (var section in sections)
                {
                    if (requirements.SectionRequirements.TryGetValue(section.Key, out var sectionReq))
                    {
                        await ValidateSectionRequirements(file.Path, section.Key, section.Value, sectionReq, result);
                    }
                }

                // Validate word count
                if (requirements.MinimumWordCount > 0)
                {
                    var wordCount = CountWords(file.Content);
                    if (wordCount < requirements.MinimumWordCount)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.InsufficientContent,
                            Component = file.Path,
                            Message = $"Document has {wordCount} words, minimum required is {requirements.MinimumWordCount}",
                            Severity = IssueSeverity.Warning
                        });
                    }
                }

                // Validate examples if required
                if (requirements.RequiresExamples && !HasCodeExamples(file.Content))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingExamples,
                        Component = file.Path,
                        Message = "Document requires code examples but none were found",
                        Severity = IssueSeverity.Error
                    });
                }

                // Validate diagrams if required
                if (requirements.RequiresDiagrams && !HasDiagrams(file.Content))
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.MissingDiagrams,
                        Component = file.Path,
                        Message = "Document requires diagrams but none were found",
                        Severity = IssueSeverity.Error
                    });
                }
            }
        }

        private async Task ValidateSectionRequirements(string filePath, string sectionName, string content, SectionRequirement req, ValidationResult result)
        {
            // Check word count
            if (req.MinimumWordCount > 0)
            {
                var wordCount = CountWords(content);
                if (wordCount < req.MinimumWordCount)
                {
                    result.Issues.Add(new DocumentationIssue
                    {
                        Type = IssueType.InsufficientContent,
                        Component = $"{filePath}#{sectionName}",
                        Message = $"Section has {wordCount} words, minimum required is {req.MinimumWordCount}",
                        Severity = IssueSeverity.Warning
                    });
                }
            }

            // Check for required code examples
            if (req.RequiresCode && !HasCodeExamples(content))
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingExamples,
                    Component = $"{filePath}#{sectionName}",
                    Message = "Section requires code examples but none were found",
                    Severity = IssueSeverity.Error
                });
            }

            // Check for required diagrams
            if (req.RequiresDiagram && !HasDiagrams(content))
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingDiagrams,
                    Component = $"{filePath}#{sectionName}",
                    Message = "Section requires diagrams but none were found",
                    Severity = IssueSeverity.Error
                });
            }

            // Check for required subsections
            var subsections = ExtractSubsections(content);
            var missingSubsections = req.RequiredSubsections.Except(subsections);
            foreach (var subsection in missingSubsections)
            {
                result.Issues.Add(new DocumentationIssue
                {
                    Type = IssueType.MissingSection,
                    Component = $"{filePath}#{sectionName}",
                    Message = $"Missing required subsection: {subsection}",
                    Severity = IssueSeverity.Error
                });
            }
        }

        private async Task ValidateVersioning(DocumentationSet docs, ValidationResult result)
        {
            foreach (var file in docs.MarkdownFiles)
            {
                var docType = DetermineDocumentationType(file);
                if (_standard.Requirements.TryGetValue(docType.ToString(), out var requirements) && requirements.RequiresVersioning)
                {
                    var versionInfo = await _versionManager.GetVersionInfoAsync(file.Path);
                    if (versionInfo == null)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.MissingVersion,
                            Component = file.Path,
                            Message = "Document requires version information but none was found",
                            Severity = IssueSeverity.Error
                        });
                    }
                    else if (!versionInfo.IsValid)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.InvalidVersion,
                            Component = file.Path,
                            Message = $"Invalid version information: {versionInfo.Error}",
                            Severity = IssueSeverity.Error
                        });
                    }
                }
            }
        }

        private async Task ValidateCustomRules(DocumentationSet docs, ValidationResult result)
        {
            foreach (var rule in _standard.ValidationRules.CustomRules.Where(r => r.IsEnabled))
            {
                foreach (var file in docs.MarkdownFiles)
                {
                    try
                    {
                        var matches = Regex.Matches(file.Content, rule.ValidationExpression);
                        if (matches.Count > 0)
                        {
                            result.Issues.Add(new DocumentationIssue
                            {
                                Type = IssueType.CustomRuleViolation,
                                Component = file.Path,
                                Message = rule.ErrorMessage,
                                Severity = rule.Severity
                            });
                        }
                    }
                    catch (RegexException ex)
                    {
                        result.Issues.Add(new DocumentationIssue
                        {
                            Type = IssueType.ValidationError,
                            Component = file.Path,
                            Message = $"Error in custom rule '{rule.Name}': {ex.Message}",
                            Severity = IssueSeverity.Error
                        });
                    }
                }
            }
        }

        private Dictionary<string, string> ExtractFrontMatter(string content)
        {
            var frontMatter = new Dictionary<string, string>();
            var match = Regex.Match(content, @"^---\s*\n([\s\S]*?)\n---\s*\n");
            if (match.Success)
            {
                var lines = match.Groups[1].Value.Split('\n');
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        frontMatter[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            return frontMatter;
        }

        private List<string> ExtractLinks(string content)
        {
            var links = new List<string>();
            var matches = Regex.Matches(content, @"\[([^\]]+)\]\(([^\)]+)\)");
            foreach (Match match in matches)
            {
                links.Add(match.Groups[2].Value);
            }
            return links;
        }

        private bool IsExternalLink(string link)
        {
            return link.StartsWith("http://") || link.StartsWith("https://");
        }

        private Dictionary<string, string> ExtractSections(string content)
        {
            var sections = new Dictionary<string, string>();
            var matches = Regex.Matches(content, @"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
            for (int i = 0; i < matches.Count; i++)
            {
                var currentMatch = matches[i];
                var sectionLevel = currentMatch.Groups[1].Length;
                var sectionName = currentMatch.Groups[2].Value.Trim();
                var sectionEnd = (i < matches.Count - 1) ? matches[i + 1].Index : content.Length;
                var sectionStart = currentMatch.Index + currentMatch.Length;
                var sectionContent = content.Substring(sectionStart, sectionEnd - sectionStart).Trim();
                sections[sectionName] = sectionContent;
            }
            return sections;
        }

        private List<string> ExtractSubsections(string content)
        {
            var subsections = new List<string>();
            var matches = Regex.Matches(content, @"^(#{2,6})\s+(.+)$", RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                subsections.Add(match.Groups[2].Value.Trim());
            }
            return subsections;
        }

        private int CountWords(string content)
        {
            return Regex.Matches(content, @"\b\w+\b").Count;
        }

        private bool HasCodeExamples(string content)
        {
            return Regex.IsMatch(content, @"```\w*\n[\s\S]*?\n```");
        }

        private bool HasDiagrams(string content)
        {
            return content.Contains("```plantuml") || content.Contains("```mermaid");
        }

        private DocumentationType DetermineDocumentationType(MarkdownFile file)
        {
            var frontMatter = ExtractFrontMatter(file.Content);
            if (frontMatter.TryGetValue("type", out var type) && 
                Enum.TryParse<DocumentationType>(type, true, out var docType))
            {
                return docType;
            }

            // Try to infer from file path or content
            if (file.Path.Contains("/api/", StringComparison.OrdinalIgnoreCase))
                return DocumentationType.API;
            if (file.Path.Contains("/architecture/", StringComparison.OrdinalIgnoreCase))
                return DocumentationType.Architecture;
            if (file.Path.Contains("/tutorials/", StringComparison.OrdinalIgnoreCase))
                return DocumentationType.Tutorial;

            // Default to Implementation if type cannot be determined
            return DocumentationType.Implementation;
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