using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Represents the result of a documentation validation operation
    /// </summary>
    public class DocumentationValidationResult
    {
        public bool IsValid { get; set;     /// <summary>
    /// Defines the documentation standards and requirements for different documentation types
    /// </summary>
    public class DocumentationStandard
    {
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<DocumentationType> SupportedTypes { get; set; } = new List<DocumentationType>();
        public Dictionary<string, DocumentationRequirement> Requirements { get; set; } = new Dictionary<string, DocumentationRequirement>();
        public StyleGuide StyleGuide { get; set; } = new StyleGuide();
        public List<string> ValidLanguages { get; set; } = new List<string>();
        public ValidationRules ValidationRules { get; set; } = new ValidationRules();
    }

    /// <summary>
    /// Supported documentation types
    /// </summary>
    public enum DocumentationType
    {
        API,
        Implementation,
        Architecture,
        Tutorial,
        PluginGuide,
        SecurityGuidelines,
        PerformanceGuidelines,
        ResourceManagement,
        TestingGuidelines
    }

    /// <summary>
    /// Defines requirements for a specific type of documentation
    /// </summary>
    public class DocumentationRequirement
    {
        public DocumentationType Type { get; set; }
        public List<string> RequiredSections { get; set; } = new List<string>();
        public Dictionary<string, SectionRequirement> SectionRequirements { get; set; } = new Dictionary<string, SectionRequirement>();
        public List<string> RequiredMetadata { get; set; } = new List<string>();
        public List<string> OptionalSections { get; set; } = new List<string>();
        public bool RequiresVersioning { get; set; }
        public bool RequiresExamples { get; set; }
        public bool RequiresDiagrams { get; set; }
        public int MinimumWordCount { get; set; }
    }

    /// <summary>
    /// Requirements for a specific documentation section
    /// </summary>
    public class SectionRequirement
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public int MinimumWordCount { get; set; }
        public bool RequiresCode { get; set; }
        public bool RequiresDiagram { get; set; }
        public List<string> RequiredSubsections { get; set; } = new List<string>();
        public List<string> RequiredMetadata { get; set; } = new List<string>();
    }

    /// <summary>
    /// Documentation style guide and formatting rules
    /// </summary>
    public class StyleGuide
    {
        public MarkdownRules MarkdownRules { get; set; } = new MarkdownRules();
        public CodeExampleRules CodeExampleRules { get; set; } = new CodeExampleRules();
        public DiagramRules DiagramRules { get; set; } = new DiagramRules();
        public List<string> ProhibitedTerms { get; set; } = new List<string>();
        public Dictionary<string, string> TermReplacements { get; set; } = new Dictionary<string, string>();
        public List<string> Abbreviations { get; set; } = new List<string>();
        public List<LinkValidationRule> LinkRules { get; set; } = new List<LinkValidationRule>();
    }

    /// <summary>
    /// Rules for markdown formatting
    /// </summary>
    public class MarkdownRules
    {
        public int MaxHeaderDepth { get; set; } = 4;
        public bool RequireHeaderSpacing { get; set; } = true;
        public bool RequireBlankLinesAroundHeaders { get; set; } = true;
        public bool RequireBlankLinesAroundLists { get; set; } = true;
        public int MaxLineLength { get; set; } = 120;
        public bool EnforceAtxHeaders { get; set; } = true;
        public bool RequireFencedCodeBlocks { get; set; } = true;
        public List<string> RequiredFrontMatter { get; set; } = new List<string>();
    }

    /// <summary>
    /// Rules for code examples
    /// </summary>
    public class CodeExampleRules
    {
        public bool RequireLanguageSpecifier { get; set; } = true;
        public bool RequireDescription { get; set; } = true;
        public bool ValidateCodeCompilation { get; set; } = true;
        public int MaxExampleLength { get; set; } = 100;
        public List<string> RequiredComments { get; set; } = new List<string>();
        public Dictionary<string, List<string>> LanguageSpecificRules { get; set; } = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// Rules for diagrams
    /// </summary>
    public class DiagramRules
    {
        public List<string> SupportedFormats { get; set; } = new List<string>();
        public bool RequireTitle { get; set; } = true;
        public bool RequireDescription { get; set; } = true;
        public bool ValidateReferences { get; set; } = true;
        public int MaxDiagramComplexity { get; set; } = 50;
        public Dictionary<string, List<string>> FormatSpecificRules { get; set; } = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// Rules for link validation
    /// </summary>
    public class LinkValidationRule
    {
        public string Pattern { get; set; }
        public bool AllowExternal { get; set; }
        public bool RequireHttps { get; set; }
        public List<string> AllowedDomains { get; set; } = new List<string>();
        public bool ValidateTarget { get; set; }
    }

    /// <summary>
    /// Rules for documentation validation
    /// </summary>
    public class ValidationRules
    {
        public bool EnforceWordCount { get; set; } = true;
        public bool EnforceCodeExamples { get; set; } = true;
        public bool EnforceDiagrams { get; set; } = true;
        public bool ValidateLinks { get; set; } = true;
        public bool ValidateCodeExamples { get; set; } = true;
        public bool ValidateCrossReferences { get; set; } = true;
        public bool EnforceVersioning { get; set; } = true;
        public List<CustomValidationRule> CustomRules { get; set; } = new List<CustomValidationRule>();
    }

    /// <summary>
    /// Custom validation rule definition
    /// </summary>
    public class CustomValidationRule
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ValidationExpression { get; set; }
        public IssueSeverity Severity { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
        public List<DocumentationIssue> Issues { get; set; } = new List<DocumentationIssue>();
        public DocumentationCoverageStats Coverage { get; set; } = new DocumentationCoverageStats();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents issues found during documentation validation
    /// </summary>
    public class DocumentationIssue
    {
        public string FilePath { get; set; }
        public string Component { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public IssueSeverity Severity { get; set; }
    }

    /// <summary>
    /// Severity levels for documentation issues
    /// </summary>
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Statistics about documentation coverage
    /// </summary>
    public class DocumentationCoverageStats
    {
        public double OverallCoverage { get; set; }
        public double PublicApiCoverage { get; set; }
        public double InterfaceCoverage { get; set; }
        public double MethodParameterCoverage { get; set; }
        public double ExampleCoverage { get; set; }
        public int TotalComponents { get; set; }
        public int DocumentedComponents { get; set; }
        public Dictionary<string, double> CoverageByNamespace { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Result of cross-reference generation
    /// </summary>
    public class CrossReferenceResult
    {
        public List<ComponentReference> References { get; set; } = new List<ComponentReference>();
        public Dictionary<string, List<string>> DependencyGraph { get; set; } = new Dictionary<string, List<string>>();
        public List<CrossReferenceIssue> Issues { get; set; } = new List<CrossReferenceIssue>();
    }

    /// <summary>
    /// Represents a reference between components
    /// </summary>
    public class ComponentReference
    {
        public string SourceComponent { get; set; }
        public string TargetComponent { get; set; }
        public string ReferenceType { get; set; }
        public string Description { get; set; }
        public string LinkUrl { get; set; }
    }

    /// <summary>
    /// Issues found during cross-reference generation
    /// </summary>
    public class CrossReferenceIssue
    {
        public string Component { get; set; }
        public string ReferencedComponent { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Result of documentation coverage analysis
    /// </summary>
    public class DocumentationCoverageResult
    {
        public DocumentationCoverageStats Coverage { get; set; } = new DocumentationCoverageStats();
        public List<DocumentationGap> Gaps { get; set; } = new List<DocumentationGap>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a gap in documentation coverage
    /// </summary>
    public class DocumentationGap
    {
        public string Component { get; set; }
        public string MissingElement { get; set; }
        public string Impact { get; set; }
        public string Recommendation { get; set; }
    }

    /// <summary>
    /// Documentation for resource management patterns
    /// </summary>
    public class ResourcePatternDocumentation
    {
        public List<ResourcePattern> Patterns { get; set; } = new List<ResourcePattern>();
        public List<ResourceUsageExample> Examples { get; set; } = new List<ResourceUsageExample>();
        public List<ResourceBestPractice> BestPractices { get; set; } = new List<ResourceBestPractice>();
    }

    /// <summary>
    /// Documentation for a resource management pattern
    /// </summary>
    public class ResourcePattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UseCase { get; set; }
        public List<string> Benefits { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }

    /// <summary>
    /// Example of resource usage patterns
    /// </summary>
    public class ResourceUsageExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public CodeExample Code { get; set; }
        public List<string> KeyPoints { get; set; } = new List<string>();
    }

    /// <summary>
    /// Best practices for resource management
    /// </summary>
    public class ResourceBestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public List<string> Guidelines { get; set; } = new List<string>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }

    /// <summary>
    /// Represents the result of a documentation generation operation
    /// </summary>
    public class DocumentationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<TypeDocumentation> Types { get; set; } = new List<TypeDocumentation>();
        public List<PluginDocumentation> Plugins { get; set; } = new List<PluginDocumentation>();
        public ValidationDocumentation Validation { get; set; }
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }

    /// <summary>
    /// Documentation for a type (class, interface, enum, etc.)
    /// </summary>
    public class TypeDocumentation
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Description { get; set; }
        public List<MethodDocumentation> Methods { get; set; } = new List<MethodDocumentation>();
        public List<PropertyDocumentation> Properties { get; set; } = new List<PropertyDocumentation>();
        public List<string> Interfaces { get; set; } = new List<string>();
    }

    /// <summary>
    /// Documentation for a method
    /// </summary>
    public class MethodDocumentation
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public string Description { get; set; }
        public List<ParameterDocumentation> Parameters { get; set; } = new List<ParameterDocumentation>();
        public List<MethodReference> References { get; set; } = new List<MethodReference>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
        public List<TestMethodReference> TestMethods { get; set; } = new List<TestMethodReference>();
    }

    /// <summary>
    /// Reference to another method that is related to this method
    /// </summary>
    public class MethodReference
    {
        public string ReferencedMethod { get; set; }
        public string ReferenceType { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Reference to a test method that tests this method
    /// </summary>
    public class TestMethodReference
    {
        public string TestClass { get; set; }
        public string TestMethod { get; set; }
        public string TestDescription { get; set; }
    }

    /// <summary>
    /// Documentation for a method parameter
    /// </summary>
    public class ParameterDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool IsOptional { get; set; }
        public string DefaultValue { get; set; }
        public List<string> Attributes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Documentation for a property
    /// </summary>
    public class PropertyDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Documentation for a plugin
    /// </summary>
    public class PluginDocumentation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public object Configuration { get; set; }
        public List<PluginInterface> Interfaces { get; set; } = new List<PluginInterface>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }

    /// <summary>
    /// Documentation for a plugin interface
    /// </summary>
    public class PluginInterface
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<MethodDocumentation> Methods { get; set; } = new List<MethodDocumentation>();
    }

    /// <summary>
    /// Documentation for code examples
    /// </summary>
    public class CodeExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public string Language { get; set; }
    }

    /// <summary>
    /// Documentation for the validation system
    /// </summary>
    public class ValidationDocumentation
    {
        public List<ValidationComponent> Components { get; set; } = new List<ValidationComponent>();
        public List<PipelineStage> Pipeline { get; set; } = new List<PipelineStage>();
        public List<ErrorPattern> ErrorHandling { get; set; } = new List<ErrorPattern>();
        public List<PerformanceConsideration> Performance { get; set; } = new List<PerformanceConsideration>();
        public List<BestPractice> BestPractices { get; set; } = new List<BestPractice>();
    }

    /// <summary>
    /// Documentation for a validation component
    /// </summary>
    public class ValidationComponent
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Purpose { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<ConfigurationOption> Configuration { get; set; } = new List<ConfigurationOption>();
    }

    /// <summary>
    /// Documentation for a pipeline stage
    /// </summary>
    public class PipelineStage
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public List<string> InputTypes { get; set; } = new List<string>();
        public List<string> OutputTypes { get; set; } = new List<string>();
        public List<string> ValidationRules { get; set; } = new List<string>();
    }

    /// <summary>
    /// Documentation for error handling patterns
    /// </summary>
    public class ErrorPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string RecoveryStrategy { get; set; }
        public List<string> ExampleScenarios { get; set; } = new List<string>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }

    /// <summary>
    /// Documentation for performance considerations
    /// </summary>
    public class PerformanceConsideration
    {
        public string Area { get; set; }
        public string Impact { get; set; }
        public string Recommendation { get; set; }
        public List<string> Metrics { get; set; } = new List<string>();
        public List<string> OptimizationStrategies { get; set; } = new List<string>();
    }

    /// <summary>
    /// Documentation for configuration options
    /// </summary>
    public class ConfigurationOption
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string DefaultValue { get; set; }
        public List<string> AllowedValues { get; set; } = new List<string>();
        public string Effect { get; set; }
    }

    /// <summary>
    /// Documentation for best practices
    /// </summary>
    public class BestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
        public List<string> References { get; set; } = new List<string>();
    }
}