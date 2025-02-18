using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class PerformanceComparison
    {
        public IEnumerable<PerformanceMetrics> BaselineMetrics { get; set; }
        public IEnumerable<PerformanceMetrics> CurrentMetrics { get; set; }
        public IEnumerable<MetricDifference> Differences { get; set; }
    }

    public class MetricDifference
    {
        public string Language { get; set; }
        public string TestType { get; set; }
        public double CpuUtilizationDiff { get; set; }
        public double MemoryUsageDiff { get; set; }
        public double ExecutionTimeDiff { get; set; }
    }
}