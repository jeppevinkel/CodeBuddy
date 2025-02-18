using System;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;
using CodeBuddy.Core.Models;

internal class PerformanceMonitor : IDisposable
{
    private readonly PerformanceTestMetricsCollector _metricsCollector;
    private bool _isCollecting;
    private bool _disposed;

    public PerformanceMonitor()
    {
        _metricsCollector = new PerformanceTestMetricsCollector();
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
            _disposed = true;
        }
    }
}

    public void Start()
    {
        ThrowIfDisposed();
    {
        _isCollecting = true;
        _metricsCollector.StartCollection();
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