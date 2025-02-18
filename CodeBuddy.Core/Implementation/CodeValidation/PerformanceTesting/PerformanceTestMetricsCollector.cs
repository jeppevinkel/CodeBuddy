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
    private readonly Dictionary<string, Queue<double>> _metricHistories;
    private readonly int _historySize = 100;

    public PerformanceTestMetricsCollector()
    {
        _executionTimer = new Stopwatch();
        _currentProcess = Process.GetCurrentProcess();
        _cpuReadings = new List<(DateTime, float)>();
        _memoryReadings = new List<(DateTime, long)>();
        _metricHistories = new Dictionary<string, Queue<double>>();

        // Initialize metric histories
        var metricKeys = new[]
        {
            "CpuUtilization",
            "MemoryUsage",
            "ResponseTime",
            "ThroughputOps",
            "ErrorRate",
            "NetworkLatency",
            "DiskIOps",
            "GcCollections",
            "ThreadPoolSize",
            "QueueLength"
        };

        foreach (var key in metricKeys)
        {
            _metricHistories[key] = new Queue<double>(_historySize);
        }

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

        // Basic execution metrics
        metrics.ExecutionTimeMs = _executionTimer.ElapsedMilliseconds;
        metrics.PeakMemoryUsageBytes = _currentProcess.PeakWorkingSet64;
        metrics.ThreadCount = _currentProcess.Threads.Count;
        metrics.HandleCount = _currentProcess.HandleCount;

        // Enhanced CPU utilization tracking
        if (_cpuCounter != null)
        {
            var cpuValue = _cpuCounter.NextValue();
            _cpuReadings.Add((timestamp, cpuValue));
            metrics.CpuUtilizationPercent = _cpuReadings.Count > 0 
                ? _cpuReadings.Average(r => r.Value) 
                : cpuValue;
            
            UpdateMetricHistory("CpuUtilization", cpuValue);
        }

        // Enhanced memory tracking
        var currentMemory = _currentProcess.WorkingSet64;
        _memoryReadings.Add((timestamp, currentMemory));
        UpdateMetricHistory("MemoryUsage", currentMemory);
        
        // Memory analysis
        if (_memoryReadings.Count >= 2)
        {
            var memoryGrowthRate = CalculateMemoryGrowthRate();
            metrics.ResourceUtilization["MemoryGrowthRateBytes"] = memoryGrowthRate;
            metrics.ResourceUtilization["MemoryTrendSlope"] = CalculateMemoryTrendSlope();
        }

        // Enhanced I/O operations tracking
        var ioOpsPerSecond = CalculateIOOperationsPerSecond();
        metrics.ResourceUtilization["ReadOperationCount"] = _currentProcess.ReadOperationCount;
        metrics.ResourceUtilization["WriteOperationCount"] = _currentProcess.WriteOperationCount;
        metrics.ResourceUtilization["IOOperationsPerSecond"] = ioOpsPerSecond;
        UpdateMetricHistory("DiskIOps", ioOpsPerSecond);

        // GC metrics
        metrics.ResourceUtilization["Gen0Collections"] = GC.CollectionCount(0);
        metrics.ResourceUtilization["Gen1Collections"] = GC.CollectionCount(1);
        metrics.ResourceUtilization["Gen2Collections"] = GC.CollectionCount(2);
        metrics.ResourceUtilization["TotalGCPauseTime"] = GC.GetTotalPauseDuration().TotalMilliseconds;
        UpdateMetricHistory("GcCollections", GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2));

        // Thread pool metrics
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        
        metrics.ResourceUtilization["WorkerThreadsInUse"] = maxWorkerThreads - workerThreads;
        metrics.ResourceUtilization["CompletionPortThreadsInUse"] = maxCompletionPortThreads - completionPortThreads;
        metrics.ResourceUtilization["ThreadPoolUtilization"] = 
            (double)(maxWorkerThreads - workerThreads) / maxWorkerThreads * 100;
        
        UpdateMetricHistory("ThreadPoolSize", maxWorkerThreads - workerThreads);

        // Historical patterns
        metrics.MemoryUsagePattern = _memoryReadings
            .Skip(Math.Max(0, _memoryReadings.Count - _historySize))
            .Select(r => r.Value)
            .ToList();

        metrics.CpuUtilizationPattern = _cpuReadings
            .Skip(Math.Max(0, _cpuReadings.Count - _historySize))
            .Select(r => r.Value)
            .ToList();

        // Add metric distributions
        foreach (var (key, history) in _metricHistories)
        {
            if (history.Count > 0)
            {
                metrics.MetricDistributions[key] = CalculateDistribution(history);
            }
        }

        // Performance anomaly detection
        DetectPerformanceAnomalies(metrics);
        DetectPerformanceIssues(metrics);

        // Cleanup old data
        CleanupOldMetrics();

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

    private void UpdateMetricHistory(string metricName, double value)
    {
        if (_metricHistories.TryGetValue(metricName, out var history))
        {
            if (history.Count >= _historySize)
            {
                history.Dequeue();
            }
            history.Enqueue(value);
        }
    }

    private double CalculateMemoryTrendSlope()
    {
        if (_memoryReadings.Count < 2)
            return 0;

        var xValues = Enumerable.Range(0, _memoryReadings.Count).Select(x => (double)x).ToList();
        var yValues = _memoryReadings.Select(r => (double)r.Value).ToList();

        var meanX = xValues.Average();
        var meanY = yValues.Average();

        var numerator = 0.0;
        var denominator = 0.0;

        for (int i = 0; i < xValues.Count; i++)
        {
            numerator += (xValues[i] - meanX) * (yValues[i] - meanY);
            denominator += Math.Pow(xValues[i] - meanX, 2);
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private Dictionary<string, double> CalculateDistribution(Queue<double> history)
    {
        var values = history.ToList();
        var sorted = values.OrderBy(v => v).ToList();
        
        return new Dictionary<string, double>
        {
            ["min"] = sorted.First(),
            ["max"] = sorted.Last(),
            ["mean"] = values.Average(),
            ["median"] = sorted[sorted.Count / 2],
            ["p95"] = sorted[(int)(sorted.Count * 0.95)],
            ["stddev"] = CalculateStandardDeviation(values)
        };
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var sumSquares = values.Sum(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

    private void DetectPerformanceAnomalies(PerformanceMetrics metrics)
    {
        foreach (var (metricName, history) in _metricHistories)
        {
            if (history.Count < 10) // Need enough data for meaningful analysis
                continue;

            var values = history.ToList();
            var mean = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            var latestValue = values.Last();

            // Check for values more than 3 standard deviations from mean (potential anomaly)
            if (Math.Abs(latestValue - mean) > 3 * stdDev)
            {
                metrics.Anomalies.Add(new PerformanceAnomaly
                {
                    MetricName = metricName,
                    Timestamp = DateTime.UtcNow,
                    ExpectedValue = mean,
                    ActualValue = latestValue,
                    Severity = "High",
                    Description = $"Anomalous {metricName} value detected: {latestValue:F2} " +
                                $"(Expected range: {mean - 3*stdDev:F2} to {mean + 3*stdDev:F2})"
                });
            }
        }
    }

    private void CleanupOldMetrics()
    {
        var threshold = DateTime.UtcNow.AddHours(-1);
        
        _cpuReadings.RemoveAll(r => r.Timestamp < threshold);
        _memoryReadings.RemoveAll(r => r.Timestamp < threshold);
        
        // Keep metric histories within size limit
        foreach (var history in _metricHistories.Values)
        {
            while (history.Count > _historySize)
            {
                history.Dequeue();
            }
        }
    }
}