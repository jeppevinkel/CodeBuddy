using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

public class MemoryLeakDetector
{
    private readonly ValidationResilienceConfig _config;
    private readonly Dictionary<string, Queue<MemorySnapshot>> _memoryHistory;
    private readonly object _lock = new object();

    public MemoryLeakDetector(ValidationResilienceConfig config)
    {
        _config = config;
        _memoryHistory = new Dictionary<string, Queue<MemorySnapshot>>();
    }

    public async Task<MemoryLeakAnalysis> AnalyzeMemoryPatterns(string componentId)
    {
        var snapshot = await CaptureMemorySnapshot(componentId);
        UpdateMemoryHistory(componentId, snapshot);
        
        return AnalyzeMemoryTrends(componentId);
    }

    private async Task<MemorySnapshot> CaptureMemorySnapshot(string componentId)
    {
        // Force a garbage collection to get accurate readings
        GC.Collect(2, GCCollectionMode.Forced, true);
        await Task.Delay(100); // Allow finalizers to run
        
        return new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            ComponentId = componentId,
            Gen0Size = GC.GetGCMemoryInfo(GCKind.Any).GenerationSizes[0],
            Gen1Size = GC.GetGCMemoryInfo(GCKind.Any).GenerationSizes[1],
            Gen2Size = GC.GetGCMemoryInfo(GCKind.Any).GenerationSizes[2],
            LohSize = GC.GetGCMemoryInfo(GCKind.Any).HeapSizeBytes,
            FinalizationQueueLength = GetFinalizationQueueLength(),
            FragmentationPercent = CalculateFragmentation()
        };
    }

    private void UpdateMemoryHistory(string componentId, MemorySnapshot snapshot)
    {
        lock (_lock)
        {
            if (!_memoryHistory.ContainsKey(componentId))
            {
                _memoryHistory[componentId] = new Queue<MemorySnapshot>();
            }

            var history = _memoryHistory[componentId];
            history.Enqueue(snapshot);

            // Keep only the recent history based on sampling rate
            int maxSamples = _config.MemorySamplingRate;
            while (history.Count > maxSamples)
            {
                history.Dequeue();
            }
        }
    }

    private MemoryLeakAnalysis AnalyzeMemoryTrends(string componentId)
    {
        Queue<MemorySnapshot> history;
        lock (_lock)
        {
            if (!_memoryHistory.TryGetValue(componentId, out history))
            {
                return new MemoryLeakAnalysis 
                { 
                    ComponentId = componentId,
                    LeakDetected = false,
                    ConfidenceLevel = 0
                };
            }
        }

        var snapshots = history.ToArray();
        if (snapshots.Length < 2) return new MemoryLeakAnalysis 
        { 
            ComponentId = componentId,
            LeakDetected = false,
            ConfidenceLevel = 0
        };

        var analysis = new MemoryLeakAnalysis
        {
            ComponentId = componentId,
            LeakDetected = false,
            StartTime = snapshots[0].Timestamp,
            EndTime = snapshots[^1].Timestamp
        };

        // Calculate growth rates
        var totalGrowthRate = CalculateGrowthRate(snapshots);
        var lohGrowthRate = CalculateLohGrowthRate(snapshots);
        var gen2GrowthRate = CalculateGen2GrowthRate(snapshots);

        // Analyze finalization queue trends
        var queueTrend = AnalyzeFinalizationQueueTrend(snapshots);

        // Calculate confidence level based on multiple indicators
        int confidenceScore = 0;
        
        if (totalGrowthRate > _config.MemoryGrowthThresholdPercent)
            confidenceScore += 30;
        
        if (lohGrowthRate > _config.LohGrowthThresholdMB)
            confidenceScore += 25;
        
        if (gen2GrowthRate > _config.MemoryGrowthThresholdPercent / 2)
            confidenceScore += 25;
        
        if (queueTrend > _config.MaxFinalizationQueueLength)
            confidenceScore += 20;

        analysis.ConfidenceLevel = confidenceScore;
        analysis.LeakDetected = confidenceScore >= _config.LeakConfidenceThreshold;
        
        if (analysis.LeakDetected && _config.EnableAutomaticMemoryDump)
        {
            TriggerMemoryDump(componentId);
        }

        return analysis;
    }

    private double CalculateGrowthRate(MemorySnapshot[] snapshots)
    {
        var first = snapshots[0];
        var last = snapshots[^1];
        var initialTotal = first.Gen0Size + first.Gen1Size + first.Gen2Size + first.LohSize;
        var finalTotal = last.Gen0Size + last.Gen1Size + last.Gen2Size + last.LohSize;
        
        return ((finalTotal - initialTotal) / (double)initialTotal) * 100;
    }

    private double CalculateLohGrowthRate(MemorySnapshot[] snapshots)
    {
        var first = snapshots[0];
        var last = snapshots[^1];
        return (last.LohSize - first.LohSize) / (1024.0 * 1024.0); // Convert to MB
    }

    private double CalculateGen2GrowthRate(MemorySnapshot[] snapshots)
    {
        var first = snapshots[0];
        var last = snapshots[^1];
        return ((last.Gen2Size - first.Gen2Size) / (double)first.Gen2Size) * 100;
    }

    private double AnalyzeFinalizationQueueTrend(MemorySnapshot[] snapshots)
    {
        return snapshots[^1].FinalizationQueueLength;
    }

    private int GetFinalizationQueueLength()
    {
        // This is an approximation as there's no direct API to get the queue length
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetGCMemoryInfo().FinalizationPendingCount;
    }

    private double CalculateFragmentation()
    {
        var info = GC.GetGCMemoryInfo();
        return ((double)info.FragmentedBytes / info.HeapSizeBytes) * 100;
    }

    private void TriggerMemoryDump(string componentId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var filename = $"memdump_{componentId}_{timestamp}.dmp";
        
        // Implementation would depend on the specific dump generation mechanism
        // Could use Windows API via P/Invoke or other platform-specific methods
    }
}

public class MemorySnapshot
{
    public DateTime Timestamp { get; set; }
    public string ComponentId { get; set; }
    public long Gen0Size { get; set; }
    public long Gen1Size { get; set; }
    public long Gen2Size { get; set; }
    public long LohSize { get; set; }
    public int FinalizationQueueLength { get; set; }
    public double FragmentationPercent { get; set; }
}

public class MemoryLeakAnalysis
{
    public string ComponentId { get; set; }
    public bool LeakDetected { get; set; }
    public int ConfidenceLevel { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Dictionary<string, string> AdditionalMetrics { get; set; } = new();
}