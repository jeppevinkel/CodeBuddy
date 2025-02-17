using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace CodeBuddy.Core.Implementation.CodeValidation;

internal class PerformanceMonitor
{
    private Process _currentProcess;
    private PerformanceCounter _cpuCounter;
    private long _initialMemory;
    private readonly ConcurrentDictionary<string, (DateTime Start, DateTime? End)> _phaseTimings = new();
    private int _concurrentOperations;
    private readonly object _concurrentLock = new();

    public void Start(string phaseName = null)
    {
        if (phaseName != null)
        {
            _phaseTimings[phaseName] = (DateTime.UtcNow, null);
            Interlocked.Increment(ref _concurrentOperations);
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

    public void End(string phaseName)
    {
        if (_phaseTimings.TryGetValue(phaseName, out var timing))
        {
            _phaseTimings[phaseName] = (timing.Start, DateTime.UtcNow);
            Interlocked.Decrement(ref _concurrentOperations);
        }
    }

    public (long PeakMemoryBytes, double CpuPercent, int ThreadCount, int HandleCount, 
            int ConcurrentOps, double ThreadPoolUtilization) GetMetrics()
    {
        var peakMemory = _currentProcess.PeakWorkingSet64;
        var cpuPercent = _cpuCounter?.NextValue() ?? 0;
        var threadCount = _currentProcess.Threads.Count;
        var handleCount = _currentProcess.HandleCount;

        var threadPoolUtilization = GetThreadPoolUtilization();

        return (peakMemory, cpuPercent, threadCount, handleCount, 
                _concurrentOperations, threadPoolUtilization);
    }

    public double CalculateParallelEfficiency()
    {
        var totalTime = TimeSpan.Zero;
        var maxEndTime = DateTime.MinValue;
        var minStartTime = DateTime.MaxValue;

        foreach (var timing in _phaseTimings)
        {
            if (!timing.Value.End.HasValue) continue;

            totalTime += timing.Value.End.Value - timing.Value.Start;
            maxEndTime = maxEndTime < timing.Value.End.Value ? timing.Value.End.Value : maxEndTime;
            minStartTime = minStartTime > timing.Value.Start ? timing.Value.Start : minStartTime;
        }

        if (maxEndTime == DateTime.MinValue || minStartTime == DateTime.MaxValue)
            return 0;

        var actualTime = maxEndTime - minStartTime;
        return (totalTime.TotalMilliseconds / actualTime.TotalMilliseconds) * 100;
    }

    private double GetThreadPoolUtilization()
    {
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

        var workerThreadsInUse = maxWorkerThreads - workerThreads;
        var portThreadsInUse = maxCompletionPortThreads - completionPortThreads;

        return ((double)(workerThreadsInUse + portThreadsInUse) / 
                (maxWorkerThreads + maxCompletionPortThreads)) * 100;
    }
}