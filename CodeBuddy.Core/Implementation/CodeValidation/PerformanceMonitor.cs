using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ParallelExecutionReport
{
    public int TotalPhases { get; set; }
    public int ParallelPhases { get; set; }
    public int MaxConcurrentPhases { get; set; }
    public Dictionary<string, PhaseTimingInfo> PhaseTimings { get; set; } = new();
}

public class PhaseTimingInfo
{
    public TimeSpan Duration { get; set; }
    public bool WasParallel { get; set; }
    public double CpuUsage { get; set; }
    public int ThreadCount { get; set; }
}

internal class PerformanceMonitor
{
    private Process _currentProcess;
    private PerformanceCounter _cpuCounter;
    private long _initialMemory;
    private readonly ConcurrentDictionary<string, (DateTime Start, DateTime? End, bool IsParallel)> _phaseTimings = new();
    private int _concurrentOperations;
    private readonly ConcurrentDictionary<string, (double CpuUsage, DateTime Timestamp, int ThreadCount)> _cpuSnapshots = new();

    public void Start(string phaseName = null, bool isParallel = false)
    {
        if (phaseName != null)
        {
            _phaseTimings[phaseName] = (DateTime.UtcNow, null, isParallel);
            if (isParallel)
            {
                Interlocked.Increment(ref _concurrentOperations);
            }
            
            _cpuSnapshots[phaseName] = (_cpuCounter?.NextValue() ?? 0, DateTime.UtcNow, _currentProcess?.Threads.Count ?? 0);
            return;
        }

        _currentProcess = Process.GetCurrentProcess();
        _initialMemory = _currentProcess.WorkingSet64;
        
        try
        {
            _cpuCounter = new PerformanceCounter("Process", "% Processor Time", _currentProcess.ProcessName);
        }
        catch (Exception)
        {
            // Performance counters might not be available in all environments
            _cpuCounter = null;
        }
    }

    public void EndPhase(string phaseName)
    {
        if (_phaseTimings.TryGetValue(phaseName, out var timing))
        {
            _phaseTimings[phaseName] = (timing.Start, DateTime.UtcNow, timing.IsParallel);
            if (timing.IsParallel)
            {
                Interlocked.Decrement(ref _concurrentOperations);
            }
        }
    }

    public ParallelExecutionReport GenerateReport()
    {
        var report = new ParallelExecutionReport
        {
            TotalPhases = _phaseTimings.Count,
            ParallelPhases = _phaseTimings.Count(x => x.Value.IsParallel),
            MaxConcurrentPhases = _phaseTimings.Values
                .Where(x => x.End.HasValue)
                .GroupBy(x => x.Start.Second)
                .Max(g => g.Count()),
            PhaseTimings = _phaseTimings.ToDictionary(
                x => x.Key,
                x => new PhaseTimingInfo
                {
                    Duration = x.Value.End?.Subtract(x.Value.Start) ?? TimeSpan.Zero,
                    WasParallel = x.Value.IsParallel,
                    CpuUsage = _cpuSnapshots.TryGetValue(x.Key, out var snapshot) ? snapshot.CpuUsage : 0,
                    ThreadCount = _cpuSnapshots.TryGetValue(x.Key, out snapshot) ? snapshot.ThreadCount : 0
                })
        };

        return report;
    }

    public (long PeakMemoryBytes, double CpuPercent, int ThreadCount, int HandleCount, int ConcurrentOps, double ThreadPoolUtilization) GetMetrics()
    {
        var peakMemory = _currentProcess.PeakWorkingSet64;
        var cpuPercent = _cpuCounter?.NextValue() ?? 0;
        var threadCount = _currentProcess.Threads.Count;
        var handleCount = _currentProcess.HandleCount;

        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
        var threadPoolUtilization = ((double)(maxWorkerThreads - workerThreads) / maxWorkerThreads) * 100;

        return (peakMemory, cpuPercent, threadCount, handleCount, _concurrentOperations, threadPoolUtilization);
    }
}