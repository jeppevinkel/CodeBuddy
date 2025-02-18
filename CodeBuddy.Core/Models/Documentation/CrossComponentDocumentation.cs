using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Documents cross-component interactions and dependencies
    /// </summary>
    public class CrossComponentDocumentation
    {
        /// <summary>
        /// Whether the documentation generation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if generation failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// List of component interactions
        /// </summary>
        public List<ComponentInteraction> Interactions { get; set; } = new();

        /// <summary>
        /// Component dependency graph
        /// </summary>
        public DependencyGraph DependencyGraph { get; set; }

        /// <summary>
        /// Interface contracts between components
        /// </summary>
        public List<InterfaceContract> Contracts { get; set; } = new();

        /// <summary>
        /// Data flow diagrams between components
        /// </summary>
        public List<DataFlowDiagram> DataFlows { get; set; } = new();

        /// <summary>
        /// Cross-component validation rules
        /// </summary>
        public List<ValidationRule> ValidationRules { get; set; } = new();
    }

    /// <summary>
    /// Represents an interaction between components
    /// </summary>
    public class ComponentInteraction
    {
        public string SourceComponent { get; set; }
        public string TargetComponent { get; set; }
        public string InteractionType { get; set; }
        public string Description { get; set; }
        public List<string> DataExchanged { get; set; } = new();
        public List<string> RequiredPermissions { get; set; } = new();
        public List<string> PotentialErrors { get; set; } = new();
        public PerformanceImpact PerformanceImpact { get; set; }
    }

    /// <summary>
    /// Represents a dependency graph between components
    /// </summary>
    public class DependencyGraph
    {
        public List<string> Nodes { get; set; } = new();
        public List<DependencyEdge> Edges { get; set; } = new();
        public Dictionary<string, List<string>> Dependencies { get; set; } = new();
        public List<CyclicDependency> CyclicDependencies { get; set; } = new();
    }

    /// <summary>
    /// Represents a contract between components through an interface
    /// </summary>
    public class InterfaceContract
    {
        public string InterfaceName { get; set; }
        public string Description { get; set; }
        public List<string> Implementations { get; set; } = new();
        public List<MethodContract> Methods { get; set; } = new();
        public List<string> UsageExamples { get; set; } = new();
        public Dictionary<string, string> ValidationRules { get; set; } = new();
    }

    /// <summary>
    /// Represents a data flow diagram between components
    /// </summary>
    public class DataFlowDiagram
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<DataFlowNode> Nodes { get; set; } = new();
        public List<DataFlowEdge> Edges { get; set; } = new();
        public List<DataTransformation> Transformations { get; set; } = new();
        public List<SecurityBoundary> SecurityBoundaries { get; set; } = new();
    }

    /// <summary>
    /// Represents a validation rule between components
    /// </summary>
    public class ValidationRule
    {
        public string RuleId { get; set; }
        public string Description { get; set; }
        public List<string> AffectedComponents { get; set; } = new();
        public string ValidationType { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        public List<string> ErrorMessages { get; set; } = new();
        public string RecoveryStrategy { get; set; }
    }
}