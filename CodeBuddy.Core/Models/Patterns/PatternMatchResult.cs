using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Models.Patterns
{
    public class PatternMatchResult
    {
        public string PatternId { get; set; }
        public string FilePath { get; set; }
        public UnifiedASTNode MatchedNode { get; set; }
        public Location Location { get; set; }
        public double ConfidenceScore { get; set; }
        public string Context { get; set; }
        public Dictionary<string, object> MatchDetails { get; set; }
        public string SuggestedFix { get; set; }
        public bool IsFuzzyMatch { get; set; }
    }

    public class Location
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
    }
}