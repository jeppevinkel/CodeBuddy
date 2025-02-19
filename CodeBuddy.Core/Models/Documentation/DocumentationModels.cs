using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    public class DocumentationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Version { get; set; }
        public List<TypeDocumentation> Types { get; set; } = new List<TypeDocumentation>();
        public List<PluginDocumentation> Plugins { get; set; } = new List<PluginDocumentation>();
        public ValidationDocumentation Validation { get; set; }
        public List<ArchitectureDecision> ArchitectureDecisions { get; set; } = new List<ArchitectureDecision>();
        public DocumentationValidationResult ValidationResult { get; set; }
        public DocumentationVersion Version { get; set; }
    }
    
    public class TypeDocumentation
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Description { get; set; }
        public List<string> Interfaces { get; set; } = new List<string>();
        public List<MethodDocumentation> Methods { get; set; } = new List<MethodDocumentation>();
        public List<PropertyDocumentation> Properties { get; set; } = new List<PropertyDocumentation>();
    }
    
    public class MethodDocumentation
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReturnType { get; set; }
        public List<ParameterDocumentation> Parameters { get; set; } = new List<ParameterDocumentation>();
        public List<MethodReference> References { get; set; } = new List<MethodReference>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
        public List<TestMethodReference> TestMethods { get; set; } = new List<TestMethodReference>();
    }
    
    public class ParameterDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool IsOptional { get; set; }
        public string DefaultValue { get; set; }
        public List<string> Attributes { get; set; } = new List<string>();
    }
    
    public class PropertyDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
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
    
    public class PluginInterface
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<MethodInfo> Methods { get; set; } = new List<MethodInfo>();
    }
    
    public class ValidationDocumentation
    {
        public List<ValidationComponent> Components { get; set; } = new List<ValidationComponent>();
        public List<PipelineStage> Pipeline { get; set; } = new List<PipelineStage>();
        public List<ErrorPattern> ErrorHandling { get; set; } = new List<ErrorPattern>();
        public List<PerformanceConsideration> Performance { get; set; } = new List<PerformanceConsideration>();
        public List<BestPractice> BestPractices { get; set; } = new List<BestPractice>();
    }
    
    public class ValidationComponent
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Responsibilities { get; set; } = new List<string>();
        public object Configuration { get; set; }
        public List<PluginInterface> Interfaces { get; set; } = new List<PluginInterface>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }
    
    public class PipelineStage
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> InputTypes { get; set; } = new List<string>();
        public List<string> OutputTypes { get; set; } = new List<string>();
        public object Configuration { get; set; }
    }
    
    public class ErrorPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> UseCases { get; set; } = new List<string>();
        public CodeExample Example { get; set; }
    }
    
    public class PerformanceConsideration
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Guidelines { get; set; } = new List<string>();
        public List<PerformanceMetric> Metrics { get; set; } = new List<PerformanceMetric>();
    }
    
    public class PerformanceMetric
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public string Notes { get; set; }
    }
    
    public class BestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Examples { get; set; } = new List<string>();
    }
    
    public class CodeExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Language { get; set; }
        public string Code { get; set; }
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
    
    public class DocumentationValidationResult
    {
        public bool IsValid { get; set; }
        public double Coverage { get; set; }
        public List<DocumentationIssue> Issues { get; set; } = new List<DocumentationIssue>();
        public List<string> Recommendations { get; set; } = new List<string>();
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
    
    public class DocumentationVersion
    {
        public string Version { get; set; }
        public DateTime Created { get; set; }
        public string Description { get; set; }
    }
    
    public class CrossReferenceResult
    {
        public List<CrossReference> References { get; set; } = new List<CrossReference>();
        public List<DocumentationIssue> Issues { get; set; } = new List<DocumentationIssue>();
    }
    
    public class CrossReference
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
    
    public class ResourcePatternDocumentation
    {
        public List<ResourcePattern> Patterns { get; set; } = new List<ResourcePattern>();
        public List<ResourceUsageExample> Examples { get; set; } = new List<ResourceUsageExample>();
        public List<ResourceBestPractice> BestPractices { get; set; } = new List<ResourceBestPractice>();
    }
    
    public class ResourcePattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UseCase { get; set; }
        public List<string> Benefits { get; set; } = new List<string>();
        public List<string> Considerations { get; set; } = new List<string>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }
    
    public class ResourceUsageExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public CodeExample Code { get; set; }
        public List<string> BestPractices { get; set; } = new List<string>();
    }
    
    public class ResourceBestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Guidelines { get; set; } = new List<string>();
        public List<CodeExample> Examples { get; set; } = new List<CodeExample>();
    }
    
    public class FeatureDocumentation
    {
        public string Name { get; set; }
        public string Content { get; set; }
    }
}