using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    public class DocumentationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Version { get; set; }
        public List<TypeDocumentation> Types { get; set; } = new();
        public List<PluginDocumentation> Plugins { get; set; } = new();
        public ValidationDocumentation Validation { get; set; }
        public List<CodeExample> Examples { get; set; } = new();
        public List<ArchitectureDecision> ArchitectureDecisions { get; set; } = new();
    }

    public class TypeDocumentation
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Description { get; set; }
        public List<MethodDocumentation> Methods { get; set; } = new();
        public List<PropertyDocumentation> Properties { get; set; } = new();
        public List<string> Interfaces { get; set; } = new();
    }

    public class MethodDocumentation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReturnType { get; set; }
        public List<ParameterDocumentation> Parameters { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
        public List<MethodReference> References { get; set; } = new();
        public List<TestMethodReference> TestMethods { get; set; } = new();
    }

    public class ParameterDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool IsOptional { get; set; }
        public string DefaultValue { get; set; }
        public List<string> Attributes { get; set; } = new();
    }

    public class PropertyDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    public class EventDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    public class CodeExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public string Language { get; set; }
    }

    public class PluginDocumentation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public object Configuration { get; set; }
        public List<PluginInterface> Interfaces { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
    }

    public class PluginInterface
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<MethodDocumentation> Methods { get; set; } = new();
        public List<EventDocumentation> Events { get; set; } = new();
    }

    public class ValidationDocumentation
    {
        public List<ValidationComponent> Components { get; set; } = new();
        public List<PipelineStage> Pipeline { get; set; } = new();
        public List<ErrorPattern> ErrorHandling { get; set; } = new();
        public List<PerformanceConsideration> Performance { get; set; } = new();
        public List<BestPractice> BestPractices { get; set; } = new();
    }

    public class ValidationComponent
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Purpose { get; set; }
        public List<string> ValidatesLanguages { get; set; } = new();
        public List<ValidationRule> ValidationRules { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
    }

    public class ValidationRule
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public CodeExample Example { get; set; }
    }

    public class PipelineStage
    {
        public string Name { get; set; }
        public int Order { get; set; }
        public string Description { get; set; }
        public string Purpose { get; set; }
        public string InputType { get; set; }
        public string OutputType { get; set; }
        public List<CodeExample> Examples { get; set; } = new();
    }

    public class ErrorPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Scenario { get; set; }
        public string Strategy { get; set; }
        public CodeExample Example { get; set; }
    }

    public class PerformanceConsideration
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public string Recommendation { get; set; }
        public List<CodeExample> Examples { get; set; } = new();
    }

    public class BestPractice
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public CodeExample Example { get; set; }
    }

    public class MethodReference
    {
        public string ReferencedMethod { get; set; }
        public string ReferenceType { get; set; }
        public string Description { get; set; }
    }

    public class TestMethodReference
    {
        public string TestClass { get; set; }
        public string TestMethod { get; set; }
        public string TestDescription { get; set; }
    }

    public class DocumentationValidationResult
    {
        public bool IsValid { get; set; }
        public double Coverage { get; set; }
        public List<DocumentationIssue> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class DocumentationIssue
    {
        public string Component { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ArchitectureDecision
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Context { get; set; }
        public string Decision { get; set; }
        public string Consequences { get; set; }
    }

    public class DocumentationMap
    {
        public string Version { get; set; }
        public DateTime Generated { get; set; }
        public Dictionary<string, List<DocFile>> Categories { get; set; } = new();
    }

    public class DocFile
    {
        public string Path { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }

    public class ResourcePattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UseCase { get; set; }
        public List<string> Benefits { get; set; } = new();
        public List<string> Considerations { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
    }

    public class ResourceUsageExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public CodeExample Code { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class ResourceBestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public List<string> Examples { get; set; } = new();
    }

    public class ResourcePatternDocumentation
    {
        public List<ResourcePattern> Patterns { get; set; } = new();
        public List<ResourceUsageExample> Examples { get; set; } = new();
        public List<ResourceBestPractice> BestPractices { get; set; } = new();
    }
}