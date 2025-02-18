using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class MiddlewareMetricsDashboard
    {
        private readonly ConcurrentDictionary<string, MiddlewareMetrics> _middlewareMetrics = new();
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly ResourceAlertManager _alertManager;
        private readonly TimeSeriesStorage _timeSeriesStorage;

        public MiddlewareMetricsDashboard(
            IMetricsAggregator metricsAggregator,
            ResourceAlertManager alertManager,
            TimeSeriesStorage timeSeriesStorage)
        {
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
            _timeSeriesStorage = timeSeriesStorage;
        }

        public async Task RecordMiddlewareExecution(string middlewareId, TimeSpan executionTime, bool success, int retryCount)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middlewareId, _ => new MiddlewareMetrics());
            
            metrics.AddExecution(executionTime, success, retryCount);
            
            // Store metrics in time series for historical analysis
            await _timeSeriesStorage.StoreMetrics(middlewareId, new Dictionary<string, double>
            {
                { "execution_time", executionTime.TotalMilliseconds },
                { "success", success ? 1 : 0 },
                { "retry_count", retryCount }
            });

            // Check for alerts
            await CheckAlertThresholds(middlewareId, metrics);
        }

        public async Task UpdateCircuitBreakerStatus(string middlewareId, bool isOpen)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middlewareId, _ => new MiddlewareMetrics());
            var previousStatus = metrics.CircuitBreakerStatus;
            metrics.CircuitBreakerStatus = isOpen;

            if (previousStatus != isOpen)
            {
                await _alertManager.RaiseAlert(new AlertModels.Alert
                {
                    Severity = AlertModels.AlertSeverity.Warning,
                    Source = middlewareId,
                    Message = $"Circuit breaker status changed to {(isOpen ? "open" : "closed")}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public async Task UpdateResourceMetrics(string middlewareId, ResourceMetrics resourceMetrics)
        {
            var metrics = _middlewareMetrics.GetOrAdd(middlewareId, _ => new MiddlewareMetrics());
            metrics.UpdateResourceMetrics(resourceMetrics);

            await _metricsAggregator.AggregateMetrics(middlewareId, resourceMetrics);
        }

        public Dictionary<string, DashboardMetrics> GetDashboardMetrics()
        {
            return _middlewareMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new DashboardMetrics
                {
                    AverageExecutionTime = kvp.Value.AverageExecutionTime,
                    SuccessRate = kvp.Value.SuccessRate,
                    AverageRetryCount = kvp.Value.AverageRetryCount,
                    CircuitBreakerStatus = kvp.Value.CircuitBreakerStatus,
                    ResourceMetrics = kvp.Value.CurrentResourceMetrics
                });
        }

        public async Task<Dictionary<string, List<HistoricalMetrics>>> GetHistoricalMetrics(
            string middlewareId, 
            DateTime start, 
            DateTime end)
        {
            return await _timeSeriesStorage.GetMetrics(middlewareId, start, end);
        }

        private async Task CheckAlertThresholds(string middlewareId, MiddlewareMetrics metrics)
        {
            var alerts = new List<AlertModels.Alert>();

            // Check execution time threshold
            if (metrics.AverageExecutionTime.TotalMilliseconds > 1000) // 1 second threshold
            {
                alerts.Add(new AlertModels.Alert
                {
                    Severity = AlertModels.AlertSeverity.Warning,
                    Source = middlewareId,
                    Message = $"High average execution time: {metrics.AverageExecutionTime.TotalMilliseconds}ms",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check success rate threshold
            if (metrics.SuccessRate < 0.95) // 95% success rate threshold
            {
                alerts.Add(new AlertModels.Alert
                {
                    Severity = AlertModels.AlertSeverity.Error,
                    Source = middlewareId,
                    Message = $"Low success rate: {metrics.SuccessRate:P2}",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check resource utilization
            if (metrics.CurrentResourceMetrics?.CpuUsage > 80) // 80% CPU threshold
            {
                alerts.Add(new AlertModels.Alert
                {
                    Severity = AlertModels.AlertSeverity.Warning,
                    Source = middlewareId,
                    Message = $"High CPU usage: {metrics.CurrentResourceMetrics.CpuUsage}%",
                    Timestamp = DateTime.UtcNow
                });
            }

            foreach (var alert in alerts)
            {
                await _alertManager.RaiseAlert(alert);
            }
        }

        private class MiddlewareMetrics
        {
            private readonly ConcurrentQueue<ExecutionRecord> _executionHistory = new();
            private const int MaxHistorySize = 1000;

            public TimeSpan AverageExecutionTime => TimeSpan.FromMilliseconds(
                _executionHistory.Any() 
                    ? _executionHistory.Average(r => r.ExecutionTime.TotalMilliseconds)
                    : 0);

            public double SuccessRate =>
                _executionHistory.Any()
                    ? _executionHistory.Count(r => r.Success) / (double)_executionHistory.Count
                    : 1.0;

            public double AverageRetryCount =>
                _executionHistory.Any()
                    ? _executionHistory.Average(r => r.RetryCount)
                    : 0;

            public bool CircuitBreakerStatus { get; set; }
            public ResourceMetrics CurrentResourceMetrics { get; private set; }

            public void AddExecution(TimeSpan executionTime, bool success, int retryCount)
            {
                _executionHistory.Enqueue(new ExecutionRecord
                {
                    ExecutionTime = executionTime,
                    Success = success,
                    RetryCount = retryCount,
                    Timestamp = DateTime.UtcNow
                });

                while (_executionHistory.Count > MaxHistorySize)
                {
                    _executionHistory.TryDequeue(out _);
                }
            }

            public void UpdateResourceMetrics(ResourceMetrics metrics)
            {
                CurrentResourceMetrics = metrics;
            }

            private class ExecutionRecord
            {
                public TimeSpan ExecutionTime { get; set; }
                public bool Success { get; set; }
                public int RetryCount { get; set; }
                public DateTime Timestamp { get; set; }
            }
        }

        public class DashboardMetrics
        {
            public TimeSpan AverageExecutionTime { get; set; }
            public double SuccessRate { get; set; }
            public double AverageRetryCount { get; set; }
            public bool CircuitBreakerStatus { get; set; }
            public ResourceMetrics ResourceMetrics { get; set; }
        }
    }
}