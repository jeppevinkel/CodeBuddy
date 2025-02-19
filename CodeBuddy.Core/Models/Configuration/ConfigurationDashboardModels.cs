using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Configuration
{
    public class ConfigurationHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string ComponentName { get; set; }
        public string Environment { get; set; }
        public string Version { get; set; }
        public List<ConfigurationValidationIssue> ValidationIssues { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class ConfigurationValidationIssue
    {
        public string IssueType { get; set; } // Error, Warning
        public string Message { get; set; }
        public string Section { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public class ConfigurationChangeEvent
    {
        public string Section { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }

    public class ConfigurationPerformanceMetrics
    {
        public double LoadTimeMs { get; set; }
        public double ValidationTimeMs { get; set; }
        public int CacheHitRate { get; set; }
        public DateTime MeasuredAt { get; set; }
    }

    public class ConfigurationUsageAnalytics
    {
        public string Section { get; set; }
        public int AccessCount { get; set; }
        public DateTime LastAccessed { get; set; }
        public List<string> AccessPatterns { get; set; }
    }

    public class ConfigurationMigrationStats
    {
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public List<string> FailureReasons { get; set; }
        public DateTime LastMigrationAttempt { get; set; }
    }
}