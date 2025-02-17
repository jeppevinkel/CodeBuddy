using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public string Language { get; set; }
    public ValidationStatistics Statistics { get; set; } = new();
}

public class ValidationIssue
{
    public string Code { get; set; }
    public string Message { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Location { get; set; }
    public string Suggestion { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    SecurityVulnerability
}

public class ValidationStatistics
{
    public int TotalIssues { get; set; }
    public int SecurityIssues { get; set; }
    public int StyleIssues { get; set; }
    public int BestPracticeIssues { get; set; }
    public double CodeCoveragePercentage { get; set; }
    public int CyclomaticComplexity { get; set; }
}