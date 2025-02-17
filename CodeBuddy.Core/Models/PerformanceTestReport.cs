using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class PerformanceTestReport
{
    public string TestName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<PerformanceTestResult> TestResults { get; set; } = new();
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class PerformanceTestResult
{
    public string Language { get; set; }
    public string TestType { get; set; }
    public DateTime Timestamp { get; set; }
    public PerformanceMetrics Metrics { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class PerformanceComparisonReport
{
    public PerformanceTestReport CurrentReport { get; set; }
    public PerformanceTestReport BaselineReport { get; set; }
    public Dictionary<string, PerformanceDifference> Differences { get; set; } = new();
}

public class PerformanceDifference
{
    public string Language { get; set; }
    public string TestType { get; set; }
    public double CpuUtilizationDelta { get; set; }
    public long MemoryUsageDelta { get; set; }
    public double ExecutionTimeDelta { get; set; }
    public bool Regression { get; set; }
}