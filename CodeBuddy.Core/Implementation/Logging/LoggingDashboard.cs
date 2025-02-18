using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Logging;

namespace CodeBuddy.Core.Implementation.Logging
{
    public class LoggingDashboard
    {
        private readonly ConcurrentDictionary<string, ComponentMetrics> _componentMetrics = new();
        private readonly ConcurrentDictionary<LogSeverity, int> _severityMetrics = new();
        private readonly ConcurrentQueue<LogEntry> _recentLogs = new();
        private readonly int _maxRecentLogs = 1000;
        private readonly TimeSpan _metricsRetention = TimeSpan.FromHours(24);
        private readonly Timer _cleanupTimer;

        public LoggingDashboard()
        {
            foreach (LogSeverity severity in Enum.GetValues(typeof(LogSeverity)))
            {
                _severityMetrics[severity] = 0;
            }

            _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        public void TrackLogEntry(LogEntry entry)
        {
            // Update severity metrics
            _severityMetrics.AddOrUpdate(entry.Severity, 1, (_, count) => count + 1);

            // Update component metrics
            var metrics = _componentMetrics.GetOrAdd(entry.ComponentName, _ => new ComponentMetrics());
            metrics.TrackEntry(entry);

            // Add to recent logs
            _recentLogs.Enqueue(entry);
            while (_recentLogs.Count > _maxRecentLogs)
            {
                _recentLogs.TryDequeue(out _);
            }
        }

        public Dictionary<string, ComponentMetricsSummary> GetComponentMetrics()
        {
            return _componentMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetSummary()
            );
        }

        public Dictionary<LogSeverity, int> GetSeverityMetrics()
        {
            return _severityMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IEnumerable<LogEntry> GetRecentLogs(int count = 100, LogSeverity? minSeverity = null)
        {
            var logs = _recentLogs.ToList();
            if (minSeverity.HasValue)
            {
                logs = logs.Where(l => l.Severity >= minSeverity.Value).ToList();
            }
            return logs.TakeLast(count);
        }

        private void CleanupOldMetrics(object state)
        {
            var cutoff = DateTime.UtcNow - _metricsRetention;

            foreach (var metrics in _componentMetrics.Values)
            {
                metrics.CleanupOldMetrics(cutoff);
            }
        }

        private class ComponentMetrics
        {
            private readonly ConcurrentDictionary<DateTime, List<LogEntry>> _hourlyLogs = new();
            private readonly ConcurrentDictionary<LogSeverity, int> _severityCounts = new();
            private long _totalLogs;
            private readonly object _lock = new();

            public ComponentMetrics()
            {
                foreach (LogSeverity severity in Enum.GetValues(typeof(LogSeverity)))
                {
                    _severityCounts[severity] = 0;
                }
            }

            public void TrackEntry(LogEntry entry)
            {
                var hour = new DateTime(entry.Timestamp.Year, entry.Timestamp.Month, entry.Timestamp.Day, entry.Timestamp.Hour, 0, 0, DateTimeKind.Utc);
                
                _hourlyLogs.AddOrUpdate(hour,
                    _ => new List<LogEntry> { entry },
                    (_, list) =>
                    {
                        lock (_lock)
                        {
                            list.Add(entry);
                            return list;
                        }
                    });

                _severityCounts.AddOrUpdate(entry.Severity, 1, (_, count) => count + 1);
                Interlocked.Increment(ref _totalLogs);
            }

            public void CleanupOldMetrics(DateTime cutoff)
            {
                var oldKeys = _hourlyLogs.Keys.Where(k => k < cutoff).ToList();
                foreach (var key in oldKeys)
                {
                    _hourlyLogs.TryRemove(key, out _);
                }
            }

            public ComponentMetricsSummary GetSummary()
            {
                return new ComponentMetricsSummary
                {
                    TotalLogs = _totalLogs,
                    SeverityCounts = _severityCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    HourlyLogCounts = _hourlyLogs.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Count
                    )
                };
            }
        }

        public class ComponentMetricsSummary
        {
            public long TotalLogs { get; set; }
            public Dictionary<LogSeverity, int> SeverityCounts { get; set; }
            public Dictionary<DateTime, int> HourlyLogCounts { get; set; }
        }
    }
}