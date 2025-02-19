using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Contains results from analyzing documentation of an entire assembly
    /// </summary>
    public class DocumentationAnalysisResult
    {
        /// <summary>
        /// The name of the analyzed assembly
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Documentation results for each type in the assembly
        /// </summary>
        public List<TypeDocumentationResult> TypeResults { get; set; }

        /// <summary>
        /// All documentation issues found during analysis
        /// </summary>
        public List<DocumentationIssue> Issues { get; set; }

        /// <summary>
        /// Documentation coverage percentage (0.0 to 1.0)
        /// </summary>
        public double Coverage { get; set; }

        /// <summary>
        /// Overall documentation quality score (0.0 to 1.0)
        /// </summary>
        public double QualityScore { get; set; }
    }

    /// <summary>
    /// Contains documentation analysis results for a specific type
    /// </summary>
    public class TypeDocumentationResult
    {
        /// <summary>
        /// The full name of the analyzed type
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Documentation results for members of this type
        /// </summary>
        public List<MemberDocumentationResult> MemberResults { get; set; }

        /// <summary>
        /// Documentation issues found for this type
        /// </summary>
        public List<DocumentationIssue> Issues { get; set; }
    }

    /// <summary>
    /// Contains documentation analysis results for a type member
    /// </summary>
    public class MemberDocumentationResult
    {
        /// <summary>
        /// Name of the member
        /// </summary>
        public string MemberName { get; set; }

        /// <summary>
        /// Type of the member (Method, Property, etc.)
        /// </summary>
        public string MemberType { get; set; }

        /// <summary>
        /// Documentation issues found for this member
        /// </summary>
        public List<DocumentationIssue> Issues { get; set; }
    }

    /// <summary>
    /// Represents an issue found in documentation
    /// </summary>
    public class DocumentationIssue
    {
        /// <summary>
        /// The type of documentation issue
        /// </summary>
        public IssueType Type { get; set; }

        /// <summary>
        /// The component where the issue was found
        /// </summary>
        public string Component { get; set; }

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Severity level of the issue
        /// </summary>
        public IssueSeverity Severity { get; set; }
    }

    /// <summary>
    /// Types of documentation issues that can be found
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Missing XML documentation comment
        /// </summary>
        MissingDescription,

        /// <summary>
        /// Missing parameter documentation
        /// </summary>
        MissingParameterDescription,

        /// <summary>
        /// Missing return value documentation
        /// </summary>
        MissingReturnDescription,

        /// <summary>
        /// Missing exception documentation
        /// </summary>
        MissingExceptionDescription,

        /// <summary>
        /// Documentation description is too short or low quality
        /// </summary>
        LowQualityDescription,

        /// <summary>
        /// Documentation contains placeholder text
        /// </summary>
        PlaceholderDocumentation,

        /// <summary>
        /// Broken cross-reference in documentation
        /// </summary>
        BrokenReference,

        /// <summary>
        /// Example code is invalid
        /// </summary>
        InvalidExample,

        /// <summary>
        /// Broken link in documentation
        /// </summary>
        BrokenLink,

        /// <summary>
        /// Invalid markdown syntax
        /// </summary>
        InvalidMarkdown,

        /// <summary>
        /// Invalid diagram syntax
        /// </summary>
        InvalidDiagram,

        /// <summary>
        /// Broken reference in diagram
        /// </summary>
        BrokenDiagramReference
    }

    /// <summary>
    /// Severity levels for documentation issues
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// Informational issue - no action required
        /// </summary>
        Info,

        /// <summary>
        /// Warning - should be fixed but not critical
        /// </summary>
        Warning,

        /// <summary>
        /// Error - must be fixed
        /// </summary>
        Error
    }
}