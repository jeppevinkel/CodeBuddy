using System.Collections.Generic;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Interface for AST validation and semantic analysis
    /// </summary>
    public interface IASTValidator
    {
        /// <summary>
        /// Validates the AST node type and its semantic correctness
        /// </summary>
        /// <param name="node">The AST node to validate</param>
        /// <returns>List of validation errors, if any</returns>
        IEnumerable<ASTValidationError> ValidateNode(UnifiedASTNode node);

        /// <summary>
        /// Performs semantic analysis on a node and its context
        /// </summary>
        /// <param name="node">The node to analyze</param>
        /// <param name="context">The semantic context</param>
        /// <returns>Analysis results containing any semantic violations</returns>
        SemanticAnalysisResult AnalyzeSemantics(UnifiedASTNode node, SemanticContext context);

        /// <summary>
        /// Validates relationships between nodes (e.g., variable declarations and usage)
        /// </summary>
        /// <param name="nodes">Related nodes to validate</param>
        /// <returns>List of relationship validation errors, if any</returns>
        IEnumerable<ASTValidationError> ValidateRelationships(IEnumerable<UnifiedASTNode> nodes);
    }

    /// <summary>
    /// Represents an error found during AST validation
    /// </summary>
    public class ASTValidationError
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public ErrorSeverity Severity { get; set; }
        public SourceLocation Location { get; set; }
    }

    /// <summary>
    /// Represents the severity of a validation error
    /// </summary>
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// Contains semantic analysis context and scope information
    /// </summary>
    public class SemanticContext
    {
        public Dictionary<string, UnifiedASTNode> Declarations { get; set; }
        public Dictionary<string, TypeInfo> TypeRegistry { get; set; }
        public Stack<ScopeContext> ScopeStack { get; set; }

        public SemanticContext()
        {
            Declarations = new Dictionary<string, UnifiedASTNode>();
            TypeRegistry = new Dictionary<string, TypeInfo>();
            ScopeStack = new Stack<ScopeContext>();
        }
    }

    /// <summary>
    /// Contains scope-specific context information
    /// </summary>
    public class ScopeContext
    {
        public string ScopeType { get; set; }
        public Dictionary<string, UnifiedASTNode> LocalDeclarations { get; set; }
        public UnifiedASTNode ScopeNode { get; set; }

        public ScopeContext()
        {
            LocalDeclarations = new Dictionary<string, UnifiedASTNode>();
        }
    }

    /// <summary>
    /// Contains type information for semantic analysis
    /// </summary>
    public class TypeInfo
    {
        public string TypeName { get; set; }
        public string Language { get; set; }
        public HashSet<string> ValidChildTypes { get; set; }
        public Dictionary<string, string> AllowedProperties { get; set; }

        public TypeInfo()
        {
            ValidChildTypes = new HashSet<string>();
            AllowedProperties = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Results of semantic analysis
    /// </summary>
    public class SemanticAnalysisResult
    {
        public bool IsValid { get; set; }
        public List<ASTValidationError> Errors { get; set; }
        public Dictionary<string, object> AnalysisMetadata { get; set; }

        public SemanticAnalysisResult()
        {
            Errors = new List<ASTValidationError>();
            AnalysisMetadata = new Dictionary<string, object>();
        }
    }
}