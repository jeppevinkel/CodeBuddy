using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    public class DocumentationTrend
    {
        public DateTime Date { get; set; }
        public double OverallCoverage { get; set; }
        public double TypeCoverage { get; set; }
        public double MethodCoverage { get; set; }
        public double PropertyCoverage { get; set; }
        public int TotalIssues { get; set; }
        public string CommitId { get; set; }
        public string Branch { get; set; }
    }

    public class DocumentationTrendAnalysis
    {
        public List<DocumentationTrend> Trends { get; set; } = new List<DocumentationTrend>();
        public double CoverageChange { get; set; }
        public int IssuesChange { get; set; }
        public bool IsImproving { get; set; }
        public List<string> Insights { get; set; } = new List<string>();
    }
}