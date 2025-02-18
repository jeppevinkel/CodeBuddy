using System;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;
using CodeBuddy.Core.Models;

internal class PerformanceMonitor : IDisposable
{
    private readonly PerformanceTestMetricsCollector _metricsCollector;
    private readonly Dictionary<string, (DateTime Timestamp, double Value)> _resourceUsageHistory;
    private readonly int _historyLimit = 100;
    private bool _isCollecting;
    private bool _disposed;
    private readonly System.Threading.Timer _resourceMonitorTimer;

    public PerformanceMonitor()
    {
        _metricsCollector = new PerformanceTestMetricsCollector();
        _resourceUsageHistory = new Dictionary<string, (DateTime, double)>();
        
        // Setup resource monitoring timer
        _resourceMonitorTimer = new System.Threading.Timer(
            MonitorResources, 
            null, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromSeconds(1));
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

            UpdateResourceHistory("Memory", timestamp, process.WorkingSet64);
            UpdateResourceHistory("CPU", timestamp, process.TotalProcessorTime.TotalMilliseconds);
            UpdateResourceHistory("Threads", timestamp, process.Threads.Count);
            UpdateResourceHistory("Handles", timestamp, process.HandleCount);
            UpdateResourceHistory("IOReads", timestamp, process.ReadOperationCount);
            UpdateResourceHistory("IOWrites", timestamp, process.WriteOperationCount);

            // Clean up old history entries
            CleanupResourceHistory();
        }
        catch (Exception)
        {
            // Ignore monitoring errors
        }
    }

    private void UpdateResourceHistory(string metric, DateTime timestamp, double value)
    {
        lock (_resourceUsageHistory)
        {
            _resourceUsageHistory[metric] = (timestamp, value);
        }
    }

    private void CleanupResourceHistory()
    {
        lock (_resourceUsageHistory)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            var keysToRemove = _resourceUsageHistory.Keys
                .Where(k => _resourceUsageHistory[k].Timestamp < cutoff)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _resourceUsageHistory.Remove(key);
            }
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