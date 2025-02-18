using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

public interface IMemoryLeakDetector
{
    Task<MemoryLeakAnalysis> AnalyzeMemoryPatternsAsync(string componentId);
    Task StartMonitoringAsync(string componentId);
    Task StopMonitoringAsync(string componentId);
    Task<IEnumerable<MemoryTrend>> GetMemoryTrendsAsync(string componentId);
    Task<MemoryHealthReport> GenerateHealthReportAsync(string componentId);
}

public class MemoryLeakDetector : IMemoryLeakDetector, IDisposable
{
    private readonly ILogger<MemoryLeakDetector> _logger;
    private readonly MemoryLeakConfig _config;
    private readonly Dictionary<string, Queue<MemorySnapshot>> _memoryHistory;
    private readonly object _lock = new object();
    private readonly Dictionary<string, Timer> _monitoringTimers;
    private readonly Dictionary<string, DateTime> _lastAlertTime;
    private readonly IMetricsAggregator _metricsAggregator;
    private readonly IResourceAlertManager _alertManager;
    private bool _disposed;

    public MemoryLeakDetector(
        ILogger<MemoryLeakDetector> logger,
        MemoryLeakConfig config,
        IMetricsAggregator metricsAggregator,
        IResourceAlertManager alertManager)
    {
        _logger = logger;
        _config = config;
        _memoryHistory = new Dictionary<string, Queue<MemorySnapshot>>();
        _monitoringTimers = new Dictionary<string, Timer>();
        _lastAlertTime = new Dictionary<string, DateTime>();
        _metricsAggregator = metricsAggregator;
        _alertManager = alertManager;
    }

    public async Task StartMonitoringAsync(string componentId)
    {
        ThrowIfDisposed();
        
        lock (_lock)
        {
            if (_monitoringTimers.ContainsKey(componentId))
            {
                return;
            }

            var timer = new Timer(async state => await MonitorMemoryAsync((string)state), 
                componentId, 
                TimeSpan.Zero, 
                TimeSpan.FromMilliseconds(_config.SamplingIntervalMs));
            
            _monitoringTimers[componentId] = timer;
        }
    }

    public async Task StopMonitoringAsync(string componentId)
    {
        ThrowIfDisposed();
        
        Timer timer;
        lock (_lock)
        {
            if (!_monitoringTimers.TryGetValue(componentId, out timer))
            {
                return;
            }
            _monitoringTimers.Remove(componentId);
        }

        await timer.DisposeAsync();
    }

    private async Task MonitorMemoryAsync(string componentId)
    {
        try
        {
            var snapshot = await CaptureMemorySnapshot(componentId);
            UpdateMemoryHistory(componentId, snapshot);

            if (_config.EnableRealTimeAnalysis)
            {
                var analysis = await AnalyzeMemoryPatternsAsync(componentId);
                await ProcessAnalysisResults(componentId, analysis);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring memory for component {ComponentId}", componentId);
        }
    }

    private async Task ProcessAnalysisResults(string componentId, MemoryLeakAnalysis analysis)
    {
        if (!analysis.LeakDetected) return;

        lock (_lock)
        {
            if (_lastAlertTime.TryGetValue(componentId, out var lastAlert) &&
                DateTime.UtcNow - lastAlert < _config.AlertCooldown)
            {
                return;
            }
            _lastAlertTime[componentId] = DateTime.UtcNow;
        }

        var alert = new ResourceAlert
        {
            ResourceType = ResourceMetricType.Memory,
            Severity = analysis.ConfidenceLevel > 90 ? AlertSeverity.Critical : AlertSeverity.Warning,
            Message = $"Potential memory leak detected in {componentId} (Confidence: {analysis.ConfidenceLevel}%)",
            Details = analysis.AdditionalMetrics,
            RecommendedAction = "Review recent code changes and monitor memory usage patterns"
        };

        await _alertManager.RaiseResourceAlert(alert);
    }

    public async Task<MemoryLeakAnalysis> AnalyzeMemoryPatternsAsync(string componentId)
    {
        var snapshot = await CaptureMemorySnapshot(componentId);
        UpdateMemoryHistory(componentId, snapshot);
        
        return AnalyzeMemoryTrends(componentId);
    }

    private async Task<MemorySnapshot> CaptureMemorySnapshot(string componentId)
    {
        ThrowIfDisposed();

        // Force a garbage collection to get accurate readings
        GC.Collect(2, GCCollectionMode.Forced, true);
        await Task.Delay(100); // Allow finalizers to run
        
        var gcInfo = GC.GetGCMemoryInfo(GCKind.Any);
        var currentProcess = Process.GetCurrentProcess();
        
        var snapshot = new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            ComponentId = componentId,
            Gen0Size = gcInfo.GenerationSizes[0],
            Gen1Size = gcInfo.GenerationSizes[1],
            Gen2Size = gcInfo.GenerationSizes[2],
            LohSize = gcInfo.HeapSizeBytes,
            FinalizationQueueLength = GetFinalizationQueueLength(),
            FragmentationPercent = CalculateFragmentation(),
            TotalAllocatedBytes = gcInfo.TotalAllocatedBytes,
            PinnedObjectsCount = gcInfo.PinnedObjectsCount,
            ProcessWorkingSet = currentProcess.WorkingSet64,
            ProcessPagedMemory = currentProcess.PagedMemorySize64,
            ProcessVirtualMemory = currentProcess.VirtualMemorySize64,
            ProcessPrivateMemory = currentProcess.PrivateMemorySize64,
            GCPauseTime = gcInfo.PauseDurations.Sum(),
            HasPendingFinalizers = (GC.WaitForPendingFinalizers(), false),
            PromotedBytes = gcInfo.PromotedBytes,
            HeapFragmentation = CalculateHeapFragmentation(gcInfo),
            ObjectsPromotedToGen1 = CalculatePromotionRate(0),
            ObjectsPromotedToGen2 = CalculatePromotionRate(1)
        };

        // Record metrics
        await _metricsAggregator.RecordMemoryMetrics(new Dictionary<string, double>
        {
            ["Gen0Size"] = snapshot.Gen0Size,
            ["Gen1Size"] = snapshot.Gen1Size,
            ["Gen2Size"] = snapshot.Gen2Size,
            ["LohSize"] = snapshot.LohSize,
            ["FragmentationPercent"] = snapshot.FragmentationPercent,
            ["PinnedObjectsCount"] = snapshot.PinnedObjectsCount,
            ["GCPauseTime"] = snapshot.GCPauseTime
        });

        return snapshot;
    }

    private void UpdateMemoryHistory(string componentId, MemorySnapshot snapshot)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_memoryHistory.ContainsKey(componentId))
            {
                _memoryHistory[componentId] = new Queue<MemorySnapshot>();
            }

            var history = _memoryHistory[componentId];
            history.Enqueue(snapshot);

            // Keep only the configured retention period
            var cutoffTime = DateTime.UtcNow.AddMinutes(-_config.HistoryRetentionMinutes);
            while (history.Count > 0 && history.Peek().Timestamp < cutoffTime)
            {
                history.Dequeue();
            }
        }
    }

    public async Task<IEnumerable<MemoryTrend>> GetMemoryTrendsAsync(string componentId)
    {
        ThrowIfDisposed();

        Queue<MemorySnapshot> history;
        lock (_lock)
        {
            if (!_memoryHistory.TryGetValue(componentId, out history))
            {
                return Enumerable.Empty<MemoryTrend>();
            }
        }

        var snapshots = history.ToList();
        if (snapshots.Count < _config.MinSamplesForAnalysis)
        {
            return Enumerable.Empty<MemoryTrend>();
        }

        return new[]
        {
            CalculateGenerationTrend(snapshots, s => s.Gen0Size, "Generation 0"),
            CalculateGenerationTrend(snapshots, s => s.Gen1Size, "Generation 1"),
            CalculateGenerationTrend(snapshots, s => s.Gen2Size, "Generation 2"),
            CalculateGenerationTrend(snapshots, s => s.LohSize, "Large Object Heap"),
            CalculateGenerationTrend(snapshots, s => s.ProcessWorkingSet, "Working Set"),
            CalculateGenerationTrend(snapshots, s => s.FragmentationPercent, "Fragmentation")
        };
    }

    private MemoryTrend CalculateGenerationTrend(List<MemorySnapshot> snapshots, Func<MemorySnapshot, double> valueSelector, string name)
    {
        var values = snapshots.Select(valueSelector).ToList();
        var growthRate = CalculateGrowthRate(values);
        var volatility = CalculateVolatility(values);

        return new MemoryTrend
        {
            Name = name,
            GrowthRate = growthRate,
            Volatility = volatility,
            CurrentValue = values.Last(),
            MinValue = values.Min(),
            MaxValue = values.Max(),
            TrendDirection = growthRate > 0.1 ? TrendDirection.Increasing :
                           growthRate < -0.1 ? TrendDirection.Decreasing :
                           TrendDirection.Stable
        };
    }

    public async Task<MemoryHealthReport> GenerateHealthReportAsync(string componentId)
    {
        ThrowIfDisposed();

        var trends = await GetMemoryTrendsAsync(componentId);
        var analysis = await AnalyzeMemoryPatternsAsync(componentId);

        return new MemoryHealthReport
        {
            ComponentId = componentId,
            GenerationTrends = trends.ToList(),
            LeakAnalysis = analysis,
            Recommendations = GenerateRecommendations(trends, analysis),
            OverallHealth = CalculateOverallHealth(trends, analysis),
            TimePeriod = new TimePeriod
            {
                Start = analysis.StartTime,
                End = analysis.EndTime
            }
        };
    }

    private IEnumerable<string> GenerateRecommendations(IEnumerable<MemoryTrend> trends, MemoryLeakAnalysis analysis)
    {
        var recommendations = new List<string>();

        foreach (var trend in trends)
        {
            if (trend.GrowthRate > 0.1)
            {
                recommendations.Add($"Investigate rising {trend.Name} usage (Growth rate: {trend.GrowthRate:P2})");
            }
            if (trend.Volatility > 0.2)
            {
                recommendations.Add($"High {trend.Name} volatility detected - review allocation patterns");
            }
        }

        if (analysis.LeakDetected)
        {
            recommendations.Add($"Memory leak detected with {analysis.ConfidenceLevel}% confidence - review recent changes");
        }

        return recommendations;
    }

    private HealthStatus CalculateOverallHealth(IEnumerable<MemoryTrend> trends, MemoryLeakAnalysis analysis)
    {
        if (analysis.LeakDetected && analysis.ConfidenceLevel > 90)
            return HealthStatus.Critical;

        var criticalTrends = trends.Count(t => t.GrowthRate > 0.2 || t.Volatility > 0.3);
        
        if (criticalTrends > 2)
            return HealthStatus.Critical;
        if (criticalTrends > 0 || analysis.LeakDetected)
            return HealthStatus.Warning;
            
        return HealthStatus.Healthy;
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
    public long TotalAllocatedBytes { get; set; }
    public long PinnedObjectsCount { get; set; }
    public long ProcessWorkingSet { get; set; }
    public long ProcessPagedMemory { get; set; }
    public long ProcessVirtualMemory { get; set; }
    public long ProcessPrivateMemory { get; set; }
    public double GCPauseTime { get; set; }
    public bool HasPendingFinalizers { get; set; }
    public long PromotedBytes { get; set; }
    public double HeapFragmentation { get; set; }
    public double ObjectsPromotedToGen1 { get; set; }
    public double ObjectsPromotedToGen2 { get; set; }
}

public class MemoryTrend
{
    public string Name { get; set; }
    public double GrowthRate { get; set; }
    public double Volatility { get; set; }
    public double CurrentValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public TrendDirection TrendDirection { get; set; }
}

public enum TrendDirection
{
    Increasing,
    Decreasing,
    Stable
}

public class MemoryHealthReport
{
    public string ComponentId { get; set; }
    public List<MemoryTrend> GenerationTrends { get; set; }
    public MemoryLeakAnalysis LeakAnalysis { get; set; }
    public List<string> Recommendations { get; set; }
    public HealthStatus OverallHealth { get; set; }
    public TimePeriod TimePeriod { get; set; }
}

public class TimePeriod
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public enum HealthStatus
{
    Healthy,
    Warning,
    Critical
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