using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

public class PerformanceMetrics
{
    public Dictionary<string, TimeSpan> PhaseTimings { get; set; } = new();
    public double AverageValidationTimeMs { get; set; }
    public long PeakMemoryUsageBytes { get; set; }
    public double CpuUtilizationPercent { get; set; }
    public Dictionary<string, double> ResourceUtilization { get; set; } = new();
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = new();
}

public class PerformanceBottleneck
{
    public string Phase { get; set; }
    public string Description { get; set; }
    public double ImpactScore { get; set; }
    public string Recommendation { get; set; }
}