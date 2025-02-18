using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Provides real-time metrics and monitoring dashboards for async operations
    /// </summary>
    public class AsyncOperationMetrics
    {
        private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics = new();
        private readonly ILogger _logger;

        public AsyncOperationMetrics(ILogger logger)
        {
            _logger = logger;
        }

        public void RecordOperation(string operationType, TimeSpan duration, long memoryUsage, bool succeeded)
        {
            var metrics = _operationMetrics.GetOrAdd(operationType, _ => new OperationMetrics());
            metrics.RecordOperation(duration, memoryUsage, succeeded);
        }

        public IReadOnlyDictionary<string, DashboardMetrics> GetDashboardMetrics()
        {
            return _operationMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new DashboardMetrics
                {
                    TotalOperations = kvp.Value.TotalOperations,
                    SuccessfulOperations = kvp.Value.SuccessfulOperations,
                    FailedOperations = kvp.Value.FailedOperations,
                    AverageDuration = kvp.Value.AverageDuration,
                    AverageMemoryUsage = kvp.Value.AverageMemoryUsage,
                    OperationsPerMinute = kvp.Value.OperationsPerMinute,
                    PeakMemoryUsage = kvp.Value.PeakMemoryUsage,
                    LastOperationTime = kvp.Value.LastOperationTime
                });
        }

        private class OperationMetrics
        {
            private readonly ConcurrentQueue<OperationRecord> _recentOperations = new();
            private long _totalOperations;
            private long _successfulOperations;
            private long _totalDurationTicks;
            private long _totalMemoryUsage;
            private long _peakMemoryUsage;
            private DateTime _lastOperationTime;

            public long TotalOperations => _totalOperations;
            public long SuccessfulOperations => _successfulOperations;
            public long FailedOperations => _totalOperations - _successfulOperations;
            
            public TimeSpan AverageDuration => _totalOperations > 0
                ? TimeSpan.FromTicks(_totalDurationTicks / _totalOperations)
                : TimeSpan.Zero;
                
            public double AverageMemoryUsage => _totalOperations > 0
                ? _totalMemoryUsage / (double)_totalOperations
                : 0;
                
            public double OperationsPerMinute
            {
                get
                {
                    CleanupOldRecords();
                    return _recentOperations.Count;
                }
            }
            
            public long PeakMemoryUsage => _peakMemoryUsage;
            public DateTime LastOperationTime => _lastOperationTime;

            public void RecordOperation(TimeSpan duration, long memoryUsage, bool succeeded)
            {
                _recentOperations.Enqueue(new OperationRecord
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = duration,
                    MemoryUsage = memoryUsage
                });

                Interlocked.Increment(ref _totalOperations);
                if (succeeded)
                {
                    Interlocked.Increment(ref _successfulOperations);
                }
                
                Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
                Interlocked.Add(ref _totalMemoryUsage, memoryUsage);
                
                while (true)
                {
                    var currentPeak = _peakMemoryUsage;
                    if (memoryUsage <= currentPeak) break;
                    if (Interlocked.CompareExchange(ref _peakMemoryUsage, memoryUsage, currentPeak) == currentPeak)
                        break;
                }

                _lastOperationTime = DateTime.UtcNow;
                CleanupOldRecords();
            }

            private void CleanupOldRecords()
            {
                var threshold = DateTime.UtcNow.AddMinutes(-1);
                while (_recentOperations.TryPeek(out var record) && record.Timestamp < threshold)
                {
                    _recentOperations.TryDequeue(out _);
                }
            }

            private class OperationRecord
            {
                public DateTime Timestamp { get; set; }
                public TimeSpan Duration { get; set; }
                public long MemoryUsage { get; set; }
            }
        }
    }

    public class DashboardMetrics
    {
        public long TotalOperations { get; set; }
        public long SuccessfulOperations { get; set; }
        public long FailedOperations { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public double AverageMemoryUsage { get; set; }
        public double OperationsPerMinute { get; set; }
        public long PeakMemoryUsage { get; set; }
        public DateTime LastOperationTime { get; set; }
    }
}