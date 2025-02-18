using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Memory;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidationContext : IDisposable
    {
        private bool _disposed;
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _timeoutCts;
        private readonly List<UnmanagedResourceHandle> _unmanagedResources;
        private readonly MemoryLeakPreventionSystem _leakPreventionSystem;
        private bool _finalizerEnabled;

        public string Id { get; } = Guid.NewGuid().ToString();
        public ICodeValidator Validator { get; set; }
        public string Code { get; set; }
        public string Language { get; set; }
        public ValidationOptions Options { get; set; }
        public MetricsCollection Metrics { get; } = new MetricsCollection();
        public ValidationResult Result { get; set; } = new ValidationResult();
        public bool IsCriticalValidation { get; set; }
        public MemoryProfile MemoryProfile { get; } = new MemoryProfile();
        public ConcurrentDictionary<string, IDisposable> ManagedResources { get; }
            = new ConcurrentDictionary<string, IDisposable>();

        public ValidationContext(MemoryLeakPreventionSystem leakPreventionSystem = null)
        {
            _leakPreventionSystem = leakPreventionSystem;
            _timeoutCts = new CancellationTokenSource();
            _unmanagedResources = new List<UnmanagedResourceHandle>();
            
            // Register finalizer only if unmanaged resources are present
            _finalizerEnabled = true;
            GC.ReRegisterForFinalize(this);

            StartResourceMonitoring();
        }

        private void StartResourceMonitoring()
        {
            if (_leakPreventionSystem != null)
            {
                MemoryProfile.UsageHistory.Add(new MemoryUsageSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ManagedMemoryBytes = GC.GetTotalMemory(false),
                    UnmanagedMemoryBytes = _unmanagedResources.Sum(r => r.Size),
                    ActiveResourceCount = ManagedResources.Count + _unmanagedResources.Count,
                    FragmentationPercent = CalculateFragmentation()
                });
            }
        }

        private double CalculateFragmentation()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var maxMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return maxMemory > 0 ? (double)totalMemory / maxMemory * 100 : 0;
        }

        public void TrackUnmanagedResource(IntPtr handle, long size, string description)
        {
            ThrowIfDisposed();
            lock (_lockObject)
            {
                _unmanagedResources.Add(new UnmanagedResourceHandle
                {
                    Handle = handle,
                    Size = size,
                    Description = description,
                    AllocationTime = DateTime.UtcNow
                });
                RecordAllocation(description, size);
            }
        }

        private void RecordAllocation(string description, long size)
        {
            if (!MemoryProfile.AllocationPatterns.TryGetValue(description, out var info))
            {
                info = new MemoryAllocationInfo
                {
                    FirstSeen = DateTime.UtcNow,
                    StackTrace = Environment.StackTrace
                };
                MemoryProfile.AllocationPatterns[description] = info;
            }

            info.TotalBytes += size;
            info.AllocationCount++;
            info.LastSeen = DateTime.UtcNow;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_lockObject)
                {
                    // Dispose managed resources with timeout protection
                    try
                    {
                        _timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
                        foreach (var resource in ManagedResources.Values)
                        {
                            if (_timeoutCts.Token.IsCancellationRequested) break;
                            resource?.Dispose();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Log timeout during cleanup
                        MemoryProfile.ResourceLeakWarnings++;
                    }
                    finally
                    {
                        ManagedResources.Clear();
                        _timeoutCts.Dispose();
                    }
                }
            }

            // Clean up unmanaged resources
            foreach (var handle in _unmanagedResources)
            {
                if (handle.Handle != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(handle.Handle);
                    }
                    catch
                    {
                        // Log cleanup failure
                        MemoryProfile.ResourceLeakWarnings++;
                    }
                }
            }
            _unmanagedResources.Clear();

            _disposed = true;
        }

        ~ValidationContext()
        {
            if (_finalizerEnabled)
            {
                Dispose(false);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ValidationContext));
            }
        }
    }

    public class UnmanagedResourceHandle
    {
        public IntPtr Handle { get; set; }
        public long Size { get; set; }
        public string Description { get; set; }
        public DateTime AllocationTime { get; set; }
    }

    public class MetricsCollection
    {
        private readonly Dictionary<string, StepMetrics> _stepMetrics = new();
        private readonly List<ResourceSnapshot> _resourceSnapshots = new();

        public void RecordStepStart(string step)
        {
            _stepMetrics[step] = new StepMetrics { StartTime = DateTime.UtcNow };
        }

        public void RecordStepEnd(string step, bool success)
        {
            if (_stepMetrics.TryGetValue(step, out var metrics))
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.Success = success;
            }
        }

        public void RecordResourceSnapshot(ResourceMetrics metrics)
        {
            _resourceSnapshots.Add(new ResourceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Metrics = metrics
            });
        }

        public IReadOnlyDictionary<string, StepMetrics> GetStepMetrics() => _stepMetrics;
        public IReadOnlyList<ResourceSnapshot> GetResourceSnapshots() => _resourceSnapshots;
    }

    public class StepMetrics
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    }

    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public ResourceMetrics Metrics { get; set; }
    }

    public class MemoryProfile
    {
        public Dictionary<string, MemoryAllocationInfo> AllocationPatterns { get; }
            = new Dictionary<string, MemoryAllocationInfo>();
        public List<MemoryUsageSnapshot> UsageHistory { get; }
            = new List<MemoryUsageSnapshot>();
        public double PeakMemoryUsage { get; set; }
        public int ResourceLeakWarnings { get; set; }
        public Dictionary<string, int> ObjectAllocationCounts { get; }
            = new Dictionary<string, int>();
    }

    public class MemoryAllocationInfo
    {
        public long TotalBytes { get; set; }
        public int AllocationCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsPotentialLeak { get; set; }
        public string StackTrace { get; set; }
    }

    public class MemoryUsageSnapshot
    {
        public DateTime Timestamp { get; set; }
        public long ManagedMemoryBytes { get; set; }
        public long UnmanagedMemoryBytes { get; set; }
        public int ActiveResourceCount { get; set; }
        public double FragmentationPercent { get; set; }
    }
}