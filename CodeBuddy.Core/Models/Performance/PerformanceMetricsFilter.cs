using System;

namespace CodeBuddy.Core.Models
{
    public class PerformanceMetricsFilter
    {
        public string Language { get; set; }
        public string TestType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class PerformanceTrendFilter : PerformanceMetricsFilter
    {
        public string GroupBy { get; set; }
        public string TrendType { get; set; }
    }

    public class PerformanceComparisonFilter
    {
        public DateTime BaselineDate { get; set; }
        public string Language { get; set; }
        public string TestType { get; set; }
    }
}