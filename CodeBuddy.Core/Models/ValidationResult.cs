namespace CodeBuddy.Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Language { get; set; } = "";
    public List<ValidationIssue> Issues { get; set; } = new();
    public ValidationStatistics Statistics { get; set; } = new();
}

public class ValidationStatistics
{
    public int TotalIssues { get; set; }
    public int SecurityIssues { get; set; }
    public int StyleIssues { get; set; }
    public int BestPracticeIssues { get; set; }
    public PerformanceMetrics Performance { get; set; } = new();
    
    // New security-specific statistics
    public int SecurityVulnerabilities { get; set; }
    public Dictionary<string, int> SecurityBreakdown { get; set; } = new();
    public Dictionary<string, double> SecurityRiskScores { get; set; } = new();
}

public class ValidationIssue
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public ValidationSeverity Severity { get; set; }
    public string Location { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> AdditionalInfo { get; set; } = new();
}

public enum ValidationSeverity
{
    Error,
    Warning,
    Info,
    SecurityVulnerability
}