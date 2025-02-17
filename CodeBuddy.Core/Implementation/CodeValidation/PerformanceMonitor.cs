using System;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;
using CodeBuddy.Core.Models;

internal class PerformanceMonitor
{
    private readonly PerformanceTestMetricsCollector _metricsCollector;
    private bool _isCollecting;

    public PerformanceMonitor()
    {
        _metricsCollector = new PerformanceTestMetricsCollector();
    }

    public void Start()
    {
        _isCollecting = true;
        _metricsCollector.StartCollection();
    }

    public async Task<(long PeakMemoryBytes, double CpuPercent, int ThreadCount, int HandleCount)> GetMetrics()
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
        return _isCollecting ? await _metricsCollector.CollectMetrics() : new PerformanceMetrics();
    }
}