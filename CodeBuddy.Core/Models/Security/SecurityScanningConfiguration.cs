using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Security
{
    public class SecurityScanningConfiguration
    {
        public bool EnableXSSDetection { get; set; } = true;
        public bool EnableSQLInjectionDetection { get; set; } = true;
        public bool EnableCommandInjectionDetection { get; set; } = true;
        public bool EnablePathTraversalDetection { get; set; } = true;
        public bool EnableDependencyScanning { get; set; } = true;
        public bool EnableSecurePatternValidation { get; set; } = true;
        public bool EnableBestPracticesValidation { get; set; } = true;

        public Dictionary<SecurityViolationCategory, SecuritySeverity> CategorySeverityOverrides { get; set; } = new();
        public Dictionary<string, SecurityRuleConfiguration> RuleConfigurations { get; set; } = new();
        
        public int MaxConcurrentScans { get; set; } = 4;
        public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxDependencyDepth { get; set; } = 5;
        
        public Dictionary<string, SecurityThreshold> Thresholds { get; set; } = new()
        {
            ["MaxMethodComplexity"] = new SecurityThreshold { Value = 10, Severity = SecuritySeverity.Medium },
            ["MaxFileSize"] = new SecurityThreshold { Value = 1000, Severity = SecuritySeverity.Low },
            ["MinPasswordLength"] = new SecurityThreshold { Value = 8, Severity = SecuritySeverity.High },
            ["MaxDatabaseConnections"] = new SecurityThreshold { Value = 100, Severity = SecuritySeverity.Medium }
        };

        public HashSet<string> ExcludedPatterns { get; set; } = new();
        public HashSet<string> ExcludedPaths { get; set; } = new();
        public HashSet<string> CriticalPaths { get; set; } = new();
    }

    public class SecurityRuleConfiguration
    {
        public string RuleId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public SecuritySeverity Severity { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Description { get; set; }
        public bool RequiresAST { get; set; }
        public string[] ApplicableLanguages { get; set; }
        public string[] Dependencies { get; set; }
        public SecurityRuleCategory Category { get; set; }
    }

    public class SecurityThreshold
    {
        public double Value { get; set; }
        public SecuritySeverity Severity { get; set; }
        public string Description { get; set; }
    }

    public enum SecurityRuleCategory
    {
        Input,
        Output,
        Authentication,
        Authorization,
        Cryptography,
        Configuration,
        ErrorHandling,
        Logging,
        DataProtection,
        CodeQuality
    }
}