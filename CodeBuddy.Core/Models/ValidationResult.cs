using System;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Models
{
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

    public class ValidationIssue
    {
        public string RuleId { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Message { get; set; }
        public Location Location { get; set; }
        public List<UnifiedASTNode> RelatedNodes { get; set; } = new List<UnifiedASTNode>();
    }

    public class ValidationResult
    {
        public string Language { get; set; }
        public ValidationStatus Status { get; set; }
        public List<ValidationIssue> Issues { get; set; }
        public UnifiedASTNode AST { get; set; }
        public List<ASTPatternMatch> PatternMatches { get; set; }
        public TimeSpan ValidationTime { get; set; }
        public ValidationErrorCollection Errors { get; set; }

        public ValidationResult()
        {
            Issues = new List<ValidationIssue>();
            PatternMatches = new List<ASTPatternMatch>();
            Errors = new ValidationErrorCollection();
            Status = ValidationStatus.Success;
        }

        public bool HasErrors => 
            Issues.Any(i => i.Severity == IssueSeverity.Error || i.Severity == IssueSeverity.Critical) ||
            Errors.HasCriticalErrors;

        public bool HasWarnings => 
            Issues.Any(i => i.Severity == IssueSeverity.Warning) ||
            Errors.ErrorCountBySeverity.ContainsKey(ErrorSeverity.Warning);

        public IEnumerable<ValidationIssue> GetIssuesBySeverity(IssueSeverity severity)
        {
            return Issues.Where(i => i.Severity == severity);
        }

        public void AddError(ValidationError error)
        {
            Errors.AddError(error);
            UpdateStatusFromError(error);
        }

        private void UpdateStatusFromError(ValidationError error)
        {
            switch (error.Severity)
            {
                case ErrorSeverity.Critical:
                case ErrorSeverity.Error:
                    Status = ValidationStatus.Error;
                    break;
                case ErrorSeverity.Warning:
                    if (Status != ValidationStatus.Error)
                        Status = ValidationStatus.Warning;
                    break;
            }
        }
    }

    public class ASTPatternMatch
    {
        public string PatternName { get; set; }
        public List<UnifiedASTNode> MatchedNodes { get; set; }
        public Location Location { get; set; }
    }
}