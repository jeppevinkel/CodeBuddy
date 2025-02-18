using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Memory;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidationContext
    {
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

        public void Dispose()
        {
            foreach (var resource in ManagedResources.Values)
            {
                resource?.Dispose();
            }
            ManagedResources.Clear();
        }
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