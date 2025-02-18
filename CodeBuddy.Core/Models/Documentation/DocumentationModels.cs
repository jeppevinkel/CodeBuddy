using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
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
    }

    /// <summary>
    /// Documentation for a method parameter
    /// </summary>
    public class ParameterDocumentation
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
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