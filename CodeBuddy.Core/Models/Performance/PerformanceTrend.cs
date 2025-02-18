namespace CodeBuddy.Core.Models
{
    public class PerformanceTrend
    {
        public string Language { get; set; }
        public string TestType { get; set; }
        public double CpuUtilizationTrend { get; set; }
        public double MemoryUsageTrend { get; set; }
        public double ExecutionTimeTrend { get; set; }
    }
}