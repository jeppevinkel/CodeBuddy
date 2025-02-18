using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Errors
{
    /// <summary>
    /// Represents the severity level of a validation error
    /// </summary>
    public enum ErrorSeverity
    {
        Critical,
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// Represents the category of a validation error
    /// </summary>
    public enum ErrorCategory
    {
        Syntax,
        Semantic,
        Resource,
        Performance,
        Security,
        Configuration,
        System
    }

    /// <summary>
    /// Base class for all validation errors
    /// </summary>
    public class ValidationError
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public ErrorSeverity Severity { get; set; }
        public ErrorCategory Category { get; set; }
        public string FilePath { get; set; }
        public int? LineNumber { get; set; }
        public int? ColumnNumber { get; set; }
        public string CodeSnippet { get; set; }
        public string ResourceId { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
        public List<ValidationError> InnerErrors { get; set; }
        public string LocalizationKey { get; set; }

        public ValidationError()
        {
            AdditionalData = new Dictionary<string, string>();
            InnerErrors = new List<ValidationError>();
        }
    }

    /// <summary>
    /// Represents a syntax error in the code
    /// </summary>
    public class SyntaxError : ValidationError
    {
        public SyntaxError()
        {
            Category = ErrorCategory.Syntax;
            Severity = ErrorSeverity.Error;
        }
    }

    /// <summary>
    /// Represents a semantic error in the code
    /// </summary>
    public class SemanticError : ValidationError
    {
        public SemanticError()
        {
            Category = ErrorCategory.Semantic;
            Severity = ErrorSeverity.Error;
        }
    }

    /// <summary>
    /// Represents a resource-related error
    /// </summary>
    public class ResourceError : ValidationError
    {
        public string ResourceType { get; set; }
        public string ResourceName { get; set; }
        public string ResourceState { get; set; }

        public ResourceError()
        {
            Category = ErrorCategory.Resource;
            Severity = ErrorSeverity.Error;
        }
    }

    /// <summary>
    /// Container for aggregated validation errors
    /// </summary>
    public class ValidationErrorCollection
    {
        public List<ValidationError> Errors { get; set; }
        public int TotalCount => Errors.Count;
        public bool HasCriticalErrors => Errors.Exists(e => e.Severity == ErrorSeverity.Critical);
        public Dictionary<ErrorSeverity, int> ErrorCountBySeverity { get; set; }
        public Dictionary<ErrorCategory, int> ErrorCountByCategory { get; set; }

        public ValidationErrorCollection()
        {
            Errors = new List<ValidationError>();
            ErrorCountBySeverity = new Dictionary<ErrorSeverity, int>();
            ErrorCountByCategory = new Dictionary<ErrorCategory, int>();
        }

        public void AddError(ValidationError error)
        {
            Errors.Add(error);
            UpdateErrorCounts(error);
        }

        private void UpdateErrorCounts(ValidationError error)
        {
            if (!ErrorCountBySeverity.ContainsKey(error.Severity))
                ErrorCountBySeverity[error.Severity] = 0;
            ErrorCountBySeverity[error.Severity]++;

            if (!ErrorCountByCategory.ContainsKey(error.Category))
                ErrorCountByCategory[error.Category] = 0;
            ErrorCountByCategory[error.Category]++;
        }
    }

    /// <summary>
    /// Provides extension methods for error handling
    /// </summary>
    public static class ValidationErrorExtensions
    {
        public static bool IsCritical(this ValidationError error)
        {
            return error.Severity == ErrorSeverity.Critical;
        }

        public static bool HasLocation(this ValidationError error)
        {
            return error.LineNumber.HasValue && error.ColumnNumber.HasValue;
        }

        public static string GetFormattedLocation(this ValidationError error)
        {
            if (!error.HasLocation())
                return "Unknown Location";
            
            return $"{error.FilePath}({error.LineNumber},{error.ColumnNumber})";
        }
    }
}