using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;

public class PerformanceTestMetricsCollector
{
    private readonly Stopwatch _executionTimer;
    private readonly Process _currentProcess;
    private readonly PerformanceCounter _cpuCounter;
    private readonly List<(DateTime Timestamp, float Value)> _cpuReadings;
    private readonly List<(DateTime Timestamp, long Value)> _memoryReadings;

    public PerformanceTestMetricsCollector()
    {
        _executionTimer = new Stopwatch();
        _currentProcess = Process.GetCurrentProcess();
        _cpuReadings = new List<(DateTime, float)>();
        _memoryReadings = new List<(DateTime, long)>();

        try
        {
            _cpuCounter = new PerformanceCounter("Process", "% Processor Time", _currentProcess.ProcessName);
        }
        catch (Exception)
        {
            _cpuCounter = null;
        }
    }

    public void StartCollection()
    {
        _executionTimer.Restart();
        _cpuReadings.Clear();
        _memoryReadings.Clear();
    }

    public async Task<PerformanceMetrics> CollectMetrics()
    {
        var timestamp = DateTime.UtcNow;
        var metrics = new PerformanceMetrics();

        // Basic metrics
        metrics.ExecutionTimeMs = _executionTimer.ElapsedMilliseconds;
        metrics.PeakMemoryUsageBytes = _currentProcess.PeakWorkingSet64;
        metrics.ThreadCount = _currentProcess.Threads.Count;
        metrics.HandleCount = _currentProcess.HandleCount;

        // CPU utilization
        if (_cpuCounter != null)
        {
            var cpuValue = _cpuCounter.NextValue();
            _cpuReadings.Add((timestamp, cpuValue));
            metrics.CpuUtilizationPercent = _cpuReadings.Count > 0 
                ? _cpuReadings.Average(r => r.Value) 
                : cpuValue;
        }

        // Memory tracking
        var currentMemory = _currentProcess.WorkingSet64;
        _memoryReadings.Add((timestamp, currentMemory));
        
        // Memory pattern analysis
        if (_memoryReadings.Count >= 2)
        {
            var memoryGrowthRate = CalculateMemoryGrowthRate();
            metrics.ResourceUtilization["MemoryGrowthRateBytes"] = memoryGrowthRate;
        }

        // I/O operations impact
        metrics.ResourceUtilization["ReadOperationCount"] = _currentProcess.ReadOperationCount;
        metrics.ResourceUtilization["WriteOperationCount"] = _currentProcess.WriteOperationCount;
        metrics.ResourceUtilization["IOOperationsPerSecond"] = CalculateIOOperationsPerSecond();

        // Add memory usage pattern
        metrics.MemoryUsagePattern = _memoryReadings
            .Skip(Math.Max(0, _memoryReadings.Count - 10))  // Keep last 10 readings
            .Select(r => r.Value)
            .ToList();

        // Add CPU utilization pattern
        metrics.CpuUtilizationPattern = _cpuReadings
            .Skip(Math.Max(0, _cpuReadings.Count - 10))  // Keep last 10 readings
            .Select(r => r.Value)
            .ToList();

        // Detect potential issues
        DetectPerformanceIssues(metrics);

        return metrics;
    }

    private double CalculateMemoryGrowthRate()
    {
        if (_memoryReadings.Count < 2) return 0;

        var first = _memoryReadings.First();
        var last = _memoryReadings.Last();
        var timeSpan = (last.Timestamp - first.Timestamp).TotalSeconds;

        if (timeSpan <= 0) return 0;

        return (last.Value - first.Value) / timeSpan;
    }

    private double CalculateIOOperationsPerSecond()
    {
        var totalOperations = _currentProcess.ReadOperationCount + _currentProcess.WriteOperationCount;
        var executionTime = _executionTimer.ElapsedMilliseconds / 1000.0;
        return executionTime > 0 ? totalOperations / executionTime : 0;
    }

    private void DetectPerformanceIssues(PerformanceMetrics metrics)
    {
        // Memory leak detection
        var memoryGrowthRate = CalculateMemoryGrowthRate();
        if (memoryGrowthRate > 1024 * 1024) // More than 1MB/s growth
        {
            metrics.Bottlenecks.Add(new PerformanceBottleneck
            {
                Phase = "Memory",
                Description = "Potential memory leak detected",
                ImpactScore = 90,
                Recommendation = "Investigate memory allocation patterns and object disposal"
            });
        }

        // CPU utilization spikes
        if (_cpuReadings.Count > 0)
        {
            var avgCpu = _cpuReadings.Average(r => r.Value);
            var maxCpu = _cpuReadings.Max(r => r.Value);
            
            if (maxCpu > avgCpu * 2 && maxCpu > 80)
            {
                metrics.Bottlenecks.Add(new PerformanceBottleneck
                {
                    Phase = "CPU",
                    Description = "CPU utilization spikes detected",
                    ImpactScore = 70,
                    Recommendation = "Review CPU-intensive operations and consider optimization or batching"
                });
            }
        }

        // I/O operations impact
        var ioOpsPerSecond = CalculateIOOperationsPerSecond();
        if (ioOpsPerSecond > 1000) // More than 1000 I/O ops per second
        {
            metrics.Bottlenecks.Add(new PerformanceBottleneck
            {
                Phase = "IO",
                Description = "High I/O operation rate detected",
                ImpactScore = 60,
                Recommendation = "Consider caching or buffering I/O operations"
            });
        }
    }
}