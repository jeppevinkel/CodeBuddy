using System;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Models.Security
{
    public enum SecurityScanStatus
    {
        Secure,
        Low,
        Medium,
        High,
        Critical,
        Error
    }

    public enum SecuritySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum SecurityViolationCategory
    {
        XSS,
        SQLInjection,
        CommandInjection,
        PathTraversal,
        WeakCrypto,
        Authentication,
        AccessControl,
        Configuration,
        ErrorHandling,
        Logging,
        DataLeakage,
        DependencyVulnerability,
        System
    }

    public class SecurityViolation
    {
        public string RuleId { get; set; }
        public SecuritySeverity Severity { get; set; }
        public SecurityViolationCategory Category { get; set; }
        public string Message { get; set; }
        public string Description { get; set; }
        public Location Location { get; set; }
        public UnifiedASTNode RelatedNode { get; set; }
        public string Recommendation { get; set; }
        public string Reference { get; set; }
        public string CWE { get; set; }
        public DateTime DetectionTime { get; set; } = DateTime.UtcNow;
    }

    public class SecurityScanResult
    {
        public string Language { get; set; }
        public SecurityScanStatus Status { get; set; }
        public List<SecurityViolation> Issues { get; set; } = new();
        public DateTime ScanStartTime { get; set; }
        public DateTime ScanEndTime { get; set; }
        public TimeSpan ScanDuration { get; set; }
        public Dictionary<SecurityViolationCategory, int> IssuesByCategory { get; set; } = new();
        public Dictionary<SecuritySeverity, int> IssuesBySeverity { get; set; } = new();
        public List<DependencyVulnerability> DependencyIssues { get; set; } = new();
        public List<string> ScannedFiles { get; set; } = new();
        public SecurityMetrics Metrics { get; set; } = new();
    }

    public class DependencyVulnerability
    {
        public string PackageName { get; set; }
        public string CurrentVersion { get; set; }
        public string VulnerableVersion { get; set; }
        public string RecommendedVersion { get; set; }
        public SecuritySeverity Severity { get; set; }
        public string Description { get; set; }
        public string CVE { get; set; }
        public List<string> AffectedFiles { get; set; } = new();
    }

    public class SecurityMetrics
    {
        public int TotalIssuesFound { get; set; }
        public int CriticalIssues { get; set; }
        public int HighIssues { get; set; }
        public int MediumIssues { get; set; }
        public int LowIssues { get; set; }
        public int VulnerableDependencies { get; set; }
        public double AverageIssuesPerFile { get; set; }
        public TimeSpan AverageScanTimePerFile { get; set; }
        public Dictionary<SecurityViolationCategory, double> CategoryDistribution { get; set; } = new();
        public Dictionary<string, double> LanguageDistribution { get; set; } = new();
        public List<TrendMetric> SecurityTrends { get; set; } = new();
    }

    public class TrendMetric
    {
        public DateTime Timestamp { get; set; }
        public string MetricName { get; set; }
        public double Value { get; set; }
        public string Category { get; set; }
    }
}