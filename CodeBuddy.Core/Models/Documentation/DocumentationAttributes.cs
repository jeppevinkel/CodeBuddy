using System;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Marks a method as a validation pipeline stage
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ValidationStageAttribute : Attribute
    {
        public string Name { get; }
        public int Order { get; }

        public ValidationStageAttribute(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }

    /// <summary>
    /// Indicates supported programming languages for a validator
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SupportedLanguagesAttribute : Attribute
    {
        public string[] Languages { get; }

        public SupportedLanguagesAttribute(params string[] languages)
        {
            Languages = languages;
        }
    }

    /// <summary>
    /// Describes a validation rule implemented by a validator
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ValidationRuleAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string Category { get; }
        public string Severity { get; }
        public string Example { get; }

        public ValidationRuleAttribute(string name, string description, string category, string severity, string example)
        {
            Name = name;
            Description = description;
            Category = category;
            Severity = severity;
            Example = example;
        }
    }

    /// <summary>
    /// Documents performance considerations for methods or types
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class PerformanceConsiderationAttribute : Attribute
    {
        public string Title { get; }
        public string Description { get; }
        public string Impact { get; }
        public string Recommendation { get; }

        public PerformanceConsiderationAttribute(string title, string description, string impact, string recommendation)
        {
            Title = title;
            Description = description;
            Impact = impact;
            Recommendation = recommendation;
        }
    }

    /// <summary>
    /// Documents best practices for types or methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class BestPracticeAttribute : Attribute
    {
        public string Title { get; }
        public string Category { get; }
        public string Description { get; }
        public string Rationale { get; }
        public string Example { get; }

        public BestPracticeAttribute(string title, string category, string description, string rationale, string example)
        {
            Title = title;
            Category = category;
            Description = description;
            Rationale = rationale;
            Example = example;
        }
    }
}