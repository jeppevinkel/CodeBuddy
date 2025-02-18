using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class MemoryMetrics
    {
        public long TotalMemoryBytes { get; set; }
        public long LargeObjectHeapBytes { get; set; }
        public long SmallObjectHeapBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MemoryLeakInfo
    {
        public string ObjectType { get; set; }
        public long InstanceCount { get; set; }
        public long TotalBytes { get; set; }
        public double ConfidenceScore { get; set; }
        public string AllocationStack { get; set; }
    }

    public class HeapAllocationHotspot
    {
        public string Location { get; set; }
        public long AllocationRate { get; set; }
        public long TotalAllocations { get; set; }
        public string StackTrace { get; set; }
    }

    public class MemoryAnalyticsReport
    {
        public List<MemoryMetrics> TimeSeriesData { get; set; }
        public List<MemoryLeakInfo> DetectedLeaks { get; set; }
        public List<HeapAllocationHotspot> Hotspots { get; set; }
        public double FragmentationIndex { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class MemoryAnalyticsConfig
    {
        public int SamplingIntervalMs { get; set; }
        public double LeakConfidenceThreshold { get; set; }
        public long MemoryThresholdBytes { get; set; }
        public bool EnableAutomaticDumps { get; set; }
    }
}