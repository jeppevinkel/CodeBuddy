using System;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

internal class PerformanceMonitor
{
    private Process _currentProcess;
    private PerformanceCounter _cpuCounter;
    private long _initialMemory;

    public void Start()
    {
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

    public (long PeakMemoryBytes, double CpuPercent, int ThreadCount, int HandleCount) GetMetrics()
    {
        var peakMemory = _currentProcess.PeakWorkingSet64;
        var cpuPercent = _cpuCounter?.NextValue() ?? 0;
        var threadCount = _currentProcess.Threads.Count;
        var handleCount = _currentProcess.HandleCount;

        return (peakMemory, cpuPercent, threadCount, handleCount);
    }
}