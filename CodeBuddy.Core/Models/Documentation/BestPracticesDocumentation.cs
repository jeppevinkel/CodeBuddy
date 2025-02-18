using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Documents best practices and implementation patterns
    /// </summary>
    public class BestPracticesDocumentation
    {
        /// <summary>
        /// Whether the documentation generation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if generation failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Component-specific best practices
        /// </summary>
        public Dictionary<string, List<BestPractice>> ComponentPractices { get; set; } = new();

        /// <summary>
        /// Common implementation patterns
        /// </summary>
        public List<ImplementationPattern> Patterns { get; set; } = new();

        /// <summary>
        /// Performance considerations and optimizations
        /// </summary>
        public List<PerformanceConsideration> PerformanceConsiderations { get; set; } = new();

        /// <summary>
        /// Error handling patterns and strategies
        /// </summary>
        public List<ErrorHandlingPattern> ErrorHandlingPatterns { get; set; } = new();

        /// <summary>
        /// Security best practices
        /// </summary>
        public List<SecurityPractice> SecurityPractices { get; set; } = new();

        /// <summary>
        /// Testing best practices
        /// </summary>
        public List<TestingPractice> TestingPractices { get; set; } = new();
    }

    /// <summary>
    /// Represents a best practice recommendation
    /// </summary>
    public class BestPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public string Category { get; set; }
        public List<string> Examples { get; set; } = new();
        public List<string> AntiPatterns { get; set; } = new();
        public List<string> References { get; set; } = new();
        public ImplementationImpact Impact { get; set; }
    }

    /// <summary>
    /// Represents a common implementation pattern
    /// </summary>
    public class ImplementationPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UseCase { get; set; }
        public List<string> Benefits { get; set; } = new();
        public List<string> Tradeoffs { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
        public List<string> RelatedPatterns { get; set; } = new();
        public PerformanceImpact PerformanceImpact { get; set; }
    }

    /// <summary>
    /// Represents a performance consideration
    /// </summary>
    public class PerformanceConsideration
    {
        public string Component { get; set; }
        public string Description { get; set; }
        public string ImpactArea { get; set; }
        public string Severity { get; set; }
        public List<string> OptimizationStrategies { get; set; } = new();
        public Dictionary<string, string> BenchmarkResults { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Represents an error handling pattern
    /// </summary>
    public class ErrorHandlingPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ErrorType { get; set; }
        public string RecoveryStrategy { get; set; }
        public List<string> PreventionMeasures { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
        public List<string> CommonPitfalls { get; set; } = new();
    }

    /// <summary>
    /// Represents a security best practice
    /// </summary>
    public class SecurityPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Risk { get; set; }
        public List<string> Mitigations { get; set; } = new();
        public List<string> ComplianceStandards { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
    }

    /// <summary>
    /// Represents a testing best practice
    /// </summary>
    public class TestingPractice
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TestingLevel { get; set; }
        public List<string> Techniques { get; set; } = new();
        public List<CodeExample> Examples { get; set; } = new();
        public List<string> Tools { get; set; } = new();
        public Dictionary<string, string> CoverageGoals { get; set; } = new();
    }

    /// <summary>
    /// Represents the impact of implementing a practice
    /// </summary>
    public class ImplementationImpact
    {
        public string Complexity { get; set; }
        public string MaintenanceEffort { get; set; }
        public string PerformanceImpact { get; set; }
        public List<string> Benefits { get; set; } = new();
        public List<string> Risks { get; set; } = new();
    }

    /// <summary>
    /// Represents performance impact of a pattern or practice
    /// </summary>
    public class PerformanceImpact
    {
        public string CPUUsage { get; set; }
        public string MemoryUsage { get; set; }
        public string NetworkImpact { get; set; }
        public string DiskImpact { get; set; }
        public string Scalability { get; set; }
        public Dictionary<string, string> Metrics { get; set; } = new();
    }
}