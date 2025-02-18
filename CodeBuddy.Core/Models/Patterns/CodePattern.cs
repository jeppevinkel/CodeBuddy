using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Models.Patterns
{
    public class CodePattern
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PatternType Type { get; set; }
        public PatternSeverity Severity { get; set; }
        public string LanguageScope { get; set; } // null means cross-language
        public string PatternExpression { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public List<string> Tags { get; set; }
        public bool IsFuzzyMatching { get; set; }
        public double MinConfidenceThreshold { get; set; }
        public string SuggestedFix { get; set; }
    }

    public enum PatternType
    {
        Structural,
        Semantic,
        Security,
        Performance,
        BestPractice,
        Custom
    }

    public enum PatternSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}