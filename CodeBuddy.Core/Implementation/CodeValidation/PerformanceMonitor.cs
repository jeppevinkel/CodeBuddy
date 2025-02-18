using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

internal class PerformanceMonitor : IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly PerformanceTestMetricsCollector _metricsCollector;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(DateTime Timestamp, double Value)>> _resourceUsageHistory;
    private readonly ResourceTrendAnalyzer _trendAnalyzer;
    private readonly int _historyLimit = 1000; // Increased for better trend analysis
    private bool _isCollecting;
    private bool _disposed;
    private readonly System.Threading.Timer _resourceMonitorTimer;
    private readonly AlertConfiguration _alertConfig;
    
    public class AlertConfiguration
    {
        public Dictionary<string, ThresholdConfig> Thresholds { get; set; } = new();
        public TimeSpan TrendAnalysisWindow { get; set; } = TimeSpan.FromMinutes(5);
        public double WarningThresholdPercent { get; set; } = 70;
        public double CriticalThresholdPercent { get; set; } = 90;
        public bool EnableAutomaticMitigation { get; set; } = true;
    }
    
    public class ThresholdConfig
    {
        public double WarningLevel { get; set; }
        public double CriticalLevel { get; set; }
        public TimeSpan SustainedDuration { get; set; } = TimeSpan.FromMinutes(1);
        public Action<ResourceAlert> OnAlert { get; set; }
    }
    
    public class ResourceAlert
    {
        public string ResourceType { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public TrendInfo Trend { get; set; }
    }
    
    public class TrendInfo
    {
        public double ChangeRate { get; set; }
        public bool IsIncreasing { get; set; }
        public TimeSpan ProjectedTimeToThreshold { get; set; }
        public double ProjectedPeakValue { get; set; }
    }
    
    public enum AlertSeverity
    {
        Warning,
        Critical
    }

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger, AlertConfiguration alertConfig = null)
    {
        _logger = logger;
        _metricsCollector = new PerformanceTestMetricsCollector();
        _resourceUsageHistory = new ConcurrentDictionary<string, ConcurrentQueue<(DateTime, double)>>();
        _alertConfig = alertConfig ?? new AlertConfiguration();
        _trendAnalyzer = new ResourceTrendAnalyzer(_alertConfig.TrendAnalysisWindow);

    public PerformanceMonitor()
    {
        _metricsCollector = new PerformanceTestMetricsCollector();
        _resourceUsageHistory = new ConcurrentDictionary<string, ConcurrentQueue<(DateTime, double)>>();
        _alertConfig = new AlertConfiguration();
        _trendAnalyzer = new ResourceTrendAnalyzer(_alertConfig.TrendAnalysisWindow);
        
        // Setup resource monitoring timer with more frequent updates for trend analysis
        _resourceMonitorTimer = new System.Threading.Timer(
            MonitorResources, 
            null, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromMilliseconds(500)); // More frequent updates
        private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PerformanceMonitor));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _isCollecting = false;
            _metricsCollector?.Dispose();
            _resourceMonitorTimer?.Dispose();
            
            lock (_resourceUsageHistory)
            {
                _resourceUsageHistory.Clear();
            }
            
            _disposed = true;
        }
    }

    public Dictionary<string, double> GetResourceTrends()
    {
        lock (_resourceUsageHistory)
        {
            var trends = new Dictionary<string, double>();
            var now = DateTime.UtcNow;

            foreach (var metric in _resourceUsageHistory.Keys)
            {
                var current = _resourceUsageHistory[metric].Value;
                var baseline = _resourceUsageHistory
                    .Where(kvp => kvp.Key == metric && kvp.Value.Timestamp > now.AddMinutes(-1))
                    .Select(kvp => kvp.Value.Value)
                    .DefaultIfEmpty(current)
                    .Average();

                trends[metric] = current - baseline;
            }

            return trends;
        }
    }
}

    public void Start()
    {
        ThrowIfDisposed();
        
        _isCollecting = true;
        _metricsCollector.StartCollection();
        _resourceUsageHistory.Clear();
    }

    private void MonitorResources(object state)
    {
        if (_disposed || !_isCollecting) return;

        try
        {
            var process = Process.GetCurrentProcess();
            var timestamp = DateTime.UtcNow;

            // Enhanced resource monitoring
            UpdateResourceHistory("Memory", timestamp, process.WorkingSet64);
            UpdateResourceHistory("PrivateMemory", timestamp, process.PrivateMemorySize64);
            UpdateResourceHistory("VirtualMemory", timestamp, process.VirtualMemorySize64);
            UpdateResourceHistory("CPU", timestamp, process.TotalProcessorTime.TotalMilliseconds);
            UpdateResourceHistory("Threads", timestamp, process.Threads.Count);
            UpdateResourceHistory("Handles", timestamp, process.HandleCount);
            UpdateResourceHistory("IOReads", timestamp, process.ReadOperationCount);
            UpdateResourceHistory("IOWrites", timestamp, process.WriteOperationCount);
            UpdateResourceHistory("PageFaults", timestamp, process.PageFaults);
            
            // GC metrics
            var gcInfo = GC.GetGCMemoryInfo();
            UpdateResourceHistory("HeapSize", timestamp, gcInfo.HeapSizeBytes);
            UpdateResourceHistory("FragmentedMemory", timestamp, gcInfo.FragmentedBytes);
            
            // Monitor thread pool usage
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            
            var threadPoolUsage = (maxWorkerThreads - workerThreads) / (double)maxWorkerThreads * 100;
            UpdateResourceHistory("ThreadPoolUsage", timestamp, threadPoolUsage);

            // Clean up old history entries
            CleanupResourceHistory();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource monitoring");
        }
    }

    private void UpdateResourceHistory(string metric, DateTime timestamp, double value)
    {
        var history = _resourceUsageHistory.GetOrAdd(metric, _ => new ConcurrentQueue<(DateTime, double)>());
        
        history.Enqueue((timestamp, value));
        
        // Trim old entries while maintaining thread safety
        while (history.Count > _historyLimit && history.TryDequeue(out _)) { }
        
        // Analyze trends and check thresholds
        AnalyzeAndAlert(metric, history);
    }

    private void CleanupResourceHistory()
    {
        var cutoff = DateTime.UtcNow - _alertConfig.TrendAnalysisWindow;
        
        foreach (var history in _resourceUsageHistory.Values)
        {
            while (history.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
            {
                history.TryDequeue(out _);
            }
        }
    }

    private void AnalyzeAndAlert(string metric, ConcurrentQueue<(DateTime Timestamp, double Value)> history)
    {
        if (!_alertConfig.Thresholds.TryGetValue(metric, out var thresholdConfig))
            return;

        var trend = _trendAnalyzer.AnalyzeTrend(history.ToArray());
        var latestValue = history.LastOrDefault().Value;

        // Check for sustained threshold violations
        if (IsThresholdSustained(history, thresholdConfig.CriticalLevel, thresholdConfig.SustainedDuration))
        {
            var alert = CreateAlert(metric, AlertSeverity.Critical, latestValue, thresholdConfig.CriticalLevel, trend);
            thresholdConfig.OnAlert?.Invoke(alert);
            _logger.LogCritical(
                "Critical resource alert for {Metric}: Current {Value:F2}, Threshold {Threshold:F2}, Trend {TrendChange:F2}%/min",
                metric, latestValue, thresholdConfig.CriticalLevel, trend.ChangeRate * 60);

            if (_alertConfig.EnableAutomaticMitigation)
            {
                TriggerAutomaticMitigation(metric, alert);
            }
        }
        else if (IsThresholdSustained(history, thresholdConfig.WarningLevel, thresholdConfig.SustainedDuration))
        {
            var alert = CreateAlert(metric, AlertSeverity.Warning, latestValue, thresholdConfig.WarningLevel, trend);
            thresholdConfig.OnAlert?.Invoke(alert);
            _logger.LogWarning(
                "Resource warning for {Metric}: Current {Value:F2}, Threshold {Threshold:F2}, Trend {TrendChange:F2}%/min",
                metric, latestValue, thresholdConfig.WarningLevel, trend.ChangeRate * 60);
        }
    }

    private bool IsThresholdSustained(ConcurrentQueue<(DateTime Timestamp, double Value)> history,
        double threshold, TimeSpan duration)
    {
        var checkTime = DateTime.UtcNow - duration;
        return history
            .Where(h => h.Timestamp >= checkTime)
            .All(h => h.Value >= threshold);
    }

    private ResourceAlert CreateAlert(string metric, AlertSeverity severity, double currentValue,
        double thresholdValue, TrendInfo trend)
    {
        return new ResourceAlert
        {
            ResourceType = metric,
            Severity = severity,
            CurrentValue = currentValue,
            ThresholdValue = thresholdValue,
            Timestamp = DateTime.UtcNow,
            Duration = _alertConfig.TrendAnalysisWindow,
            Trend = trend,
            Message = $"{severity} alert for {metric}: Current value {currentValue:F2} exceeds threshold {thresholdValue:F2}. " +
                     $"Trend shows {(trend.IsIncreasing ? "increase" : "decrease")} at {trend.ChangeRate:F2}/min"
        };
    }

    private void TriggerAutomaticMitigation(string metric, ResourceAlert alert)
    {
        try
        {
            switch (metric.ToLower())
            {
                case "memory":
                case "privatememory":
                    if (alert.Severity == AlertSeverity.Critical)
                    {
                        _logger.LogInformation("Triggering emergency memory cleanup due to critical memory alert");
                        GC.Collect(2, GCCollectionMode.Aggressive, true);
                        GC.WaitForPendingFinalizers();
                    }
                    break;

                case "threadpoolutilization":
                    if (alert.CurrentValue > 90)
                    {
                        _logger.LogInformation("Adjusting thread pool size due to high utilization");
                        ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
                        ThreadPool.SetMaxThreads(workerThreads + 4, completionPortThreads + 4);
                    }
                    break;

                case "handles":
                    if (alert.Severity == AlertSeverity.Critical)
                    {
                        _logger.LogInformation("Triggering handle cleanup due to critical handle count");
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic mitigation for {Metric}", metric);
        }
    }

    public async Task<(long PeakMemoryBytes, double CpuPercent, int ThreadCount, int HandleCount)> GetMetrics()
    {
        ThrowIfDisposed();
    {
        if (!_isCollecting)
        {
            return (0, 0, 0, 0);
        }

        var metrics = await _metricsCollector.CollectMetrics();
        return (
            metrics.PeakMemoryUsageBytes,
            metrics.CpuUtilizationPercent,
            metrics.ThreadCount,
            metrics.HandleCount
        );
    }

    public async Task<PerformanceMetrics> GetDetailedMetrics()
    {
        ThrowIfDisposed();
    {
        return _isCollecting ? await _metricsCollector.CollectMetrics() : new PerformanceMetrics();
    }
}