using System;

namespace CodeBuddy.Core.Models;

public class MemoryLeakConfig
{
    public int SamplingIntervalMs { get; set; } = 1000;
    public int HistoryRetentionMinutes { get; set; } = 30;
    public int MinSamplesForAnalysis { get; set; } = 5;
    public double Gen0GrowthThresholdPercent { get; set; } = 20;
    public double Gen1GrowthThresholdPercent { get; set; } = 15;
    public double Gen2GrowthThresholdPercent { get; set; } = 10;
    public double LohGrowthThresholdMB { get; set; } = 50;
    public double FragmentationThresholdPercent { get; set; } = 40;
    public int FinalizationQueueThreshold { get; set; } = 1000;
    public bool EnableAutomaticMemoryDump { get; set; } = true;
    public string MemoryDumpPath { get; set; } = "memory_dumps";
    public int LeakConfidenceThreshold { get; set; } = 75;
    public bool EnableRealTimeAnalysis { get; set; } = true;
    public TimeSpan AlertCooldown { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableDetailedReporting { get; set; } = true;
}