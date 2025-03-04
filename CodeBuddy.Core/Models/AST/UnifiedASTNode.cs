using System;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Models.AST
{
    /// <summary>
    /// Represents a node in the unified Abstract Syntax Tree that can represent code constructs
    /// across different programming languages. Includes semantic validation capabilities.
    /// </summary>
    public class UnifiedASTNode
    {
        private readonly IASTValidator _validator;
        private SemanticContext _semanticContext;

        /// <summary>
        /// The type of the node (e.g., Method, Class, Variable, etc.)
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// The name or identifier of the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The original source language this node was parsed from
        /// </summary>
        public string SourceLanguage { get; set; }

        /// <summary>
        /// Additional properties specific to the node type
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Child nodes in the AST
        /// </summary>
        public List<UnifiedASTNode> Children { get; set; }

        /// <summary>
        /// Source code location information
        /// </summary>
        public SourceLocation Location { get; set; }

        /// <summary>
        /// Parent node in the AST
        /// </summary>
        public UnifiedASTNode Parent { get; private set; }

        /// <summary>
        /// Semantic analysis results for this node
        /// </summary>
        public SemanticAnalysisResult SemanticAnalysis { get; private set; }

        public UnifiedASTNode(IASTValidator validator = null)
        {
            _validator = validator;
            Properties = new Dictionary<string, object>();
            Children = new List<UnifiedASTNode>();
            _semanticContext = new SemanticContext();
        }

        /// <summary>
        /// Adds a child node and sets its parent reference
        /// </summary>
        public void AddChild(UnifiedASTNode child)
        {
            Children.Add(child);
            child.Parent = this;
        }

        /// <summary>
        /// Validates the node type and its semantic correctness
        /// </summary>
        public IEnumerable<ASTValidationError> Validate()
        {
            if (_validator == null)
            {
                throw new InvalidOperationException("Validator not initialized. Please provide a validator instance.");
            }

            return _validator.ValidateNode(this);
        }

        /// <summary>
        /// Performs semantic analysis on this node and its children
        /// </summary>
        public SemanticAnalysisResult AnalyzeSemantics()
        {
            if (_validator == null)
            {
                throw new InvalidOperationException("Validator not initialized. Please provide a validator instance.");
            }

            SemanticAnalysis = _validator.AnalyzeSemantics(this, _semanticContext);
            return SemanticAnalysis;
        }

        /// <summary>
        /// Validates relationships with other nodes
        /// </summary>
        public IEnumerable<ASTValidationError> ValidateRelationships(IEnumerable<UnifiedASTNode> relatedNodes)
        {
            if (_validator == null)
            {
                throw new InvalidOperationException("Validator not initialized. Please provide a validator instance.");
            }

            return _validator.ValidateRelationships(new[] { this }.Concat(relatedNodes));
        }

        /// <summary>
        /// Sets the semantic context for this node and its children
        /// </summary>
        public void SetSemanticContext(SemanticContext context)
        {
            _semanticContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets the inferred type of this node, if available
        /// </summary>
        public string GetInferredType()
        {
            return Properties.TryGetValue("InferredType", out var type) ? type.ToString() : null;
        }

        /// <summary>
        /// Sets the inferred type for this node
        /// </summary>
        public void SetInferredType(string type)
        {
            Properties["InferredType"] = type;
        }

        /// <summary>
        /// Checks if this node has type information
        /// </summary>
        public bool HasTypeInformation()
        {
            return Properties.ContainsKey("InferredType") || 
                   Properties.ContainsKey("Type") ||
                   Properties.ContainsKey("ReturnType");
        }

        /// <summary>
        /// Gets complete type information including nullability and generics
        /// </summary>
        public TypeInformation GetTypeInformation()
        {
            var typeInfo = new TypeInformation
            {
                BaseType = GetInferredType() ?? 
                          (Properties.TryGetValue("Type", out var type) ? type.ToString() : null) ??
                          (Properties.TryGetValue("ReturnType", out var returnType) ? returnType.ToString() : null),
                IsNullable = Properties.TryGetValue("IsNullable", out var nullable) && (bool)nullable,
                GenericParameters = Properties.TryGetValue("GenericParameters", out var generics) 
                    ? ((string[])generics).ToList() 
                    : new List<string>()
            };

            if (Properties.TryGetValue("TypeConstraints", out var constraints))
            {
                typeInfo.Constraints = ((Dictionary<string, string>)constraints)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return typeInfo;
        }

        /// <summary>
        /// Gets all ancestors of this node up to the root
        /// </summary>
        public IEnumerable<UnifiedASTNode> GetAncestors()
        {
            var current = Parent;
            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Gets all descendants of this node
        /// </summary>
        public IEnumerable<UnifiedASTNode> GetDescendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var descendant in child.GetDescendants())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Finds the first ancestor of a specific type
        /// </summary>
        public UnifiedASTNode FindAncestorOfType(string nodeType)
        {
            return GetAncestors().FirstOrDefault(n => n.NodeType == nodeType);
        }

        /// <summary>
        /// Finds all descendants of a specific type
        /// </summary>
        public IEnumerable<UnifiedASTNode> FindDescendantsOfType(string nodeType)
        {
            return GetDescendants().Where(n => n.NodeType == nodeType);
        }
    }

    /// <summary>
    /// Represents the location of a node in the source code
    /// </summary>
    public class SourceLocation
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string FilePath { get; set; }
    }

    /// <summary>
    /// Represents detailed type information for a node
    /// </summary>
    public class TypeInformation
    {
        public string BaseType { get; set; }
        public bool IsNullable { get; set; }
        public List<string> GenericParameters { get; set; }
        public Dictionary<string, string> Constraints { get; set; }

        public override string ToString()
        {
            var result = BaseType;
            
            if (GenericParameters?.Any() == true)
            {
                result += $"<{string.Join(", ", GenericParameters)}>";
            }
            
            if (IsNullable)
            {
                result += "?";
            }

            return result;
        }
    }
}