using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class PerformanceMetrics
{
    // Timing metrics
    public Dictionary<string, TimeSpan> PhaseTimings { get; set; } = new();
    public double ExecutionTimeMs { get; set; }
    public double AverageValidationTimeMs { get; set; }
    
    // Resource utilization metrics
    public long PeakMemoryUsageBytes { get; set; }
    public long CurrentMemoryUsageBytes { get; set; }
    public double CpuUtilizationPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public Dictionary<string, object> ResourceUtilization { get; set; } = new();
    
    // Performance patterns
    public List<long> MemoryUsagePattern { get; set; } = new();
    public List<float> CpuUtilizationPattern { get; set; } = new();
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = new();
}

public class PerformanceBottleneck
{
    public string Phase { get; set; }
    public string Description { get; set; }
    public double ImpactScore { get; set; }
    public string Recommendation { get; set; }
}