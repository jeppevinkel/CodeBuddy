using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Models
{
    /// <summary>
    /// Represents the result of a code validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// The unified AST representation of the validated code
        /// </summary>
        public UnifiedASTNode AST { get; set; }

        /// <summary>
        /// Results from cross-language pattern matching analysis
        /// </summary>
        public List<ASTPatternMatch> PatternMatches { get; set; }

        /// <summary>
        /// List of validation issues found
        /// </summary>
        public List<ValidationIssue> Issues { get; set; }

        /// <summary>
        /// Overall validation status
        /// </summary>
        public ValidationStatus Status { get; set; }

        /// <summary>
        /// The language of the validated code
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Time taken to perform the validation
        /// </summary>
        public TimeSpan ValidationTime { get; set; }

        public ValidationResult()
        {
            Issues = new List<ValidationIssue>();
            PatternMatches = new List<ASTPatternMatch>();
            Status = ValidationStatus.Success;
        }
    }

    public class ASTPatternMatch
    {
        /// <summary>
        /// The pattern that was matched
        /// </summary>
        public string PatternName { get; set; }

        /// <summary>
        /// The nodes that matched the pattern
        /// </summary>
        public List<UnifiedASTNode> MatchedNodes { get; set; }

        /// <summary>
        /// Location where the pattern was found
        /// </summary>
        public SourceLocation Location { get; set; }

        /// <summary>
        /// Additional context about the match
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        public ASTPatternMatch()
        {
            MatchedNodes = new List<UnifiedASTNode>();
            Context = new Dictionary<string, object>();
        }
    }

    public class ValidationIssue
    {
        /// <summary>
        /// The severity of the issue
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Location in the source code where the issue was found
        /// </summary>
        public SourceLocation Location { get; set; }

        /// <summary>
        /// The rule that generated this issue
        /// </summary>
        public string RuleId { get; set; }

        /// <summary>
        /// Related AST nodes
        /// </summary>
        public List<UnifiedASTNode> RelatedNodes { get; set; }

        public ValidationIssue()
        {
            RelatedNodes = new List<UnifiedASTNode>();
        }
    }

    public enum ValidationStatus
    {
        Success,
        Warning,
        Error
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}