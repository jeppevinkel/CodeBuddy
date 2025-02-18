using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsDashboard
    {
        Task StartMonitoring(CancellationToken cancellationToken = default);
        Task StopMonitoring();
        Task<DashboardData> GetDashboardData();
        void SetAlertThreshold(string metricName, double threshold);
        IEnumerable<Alert> GetActiveAlerts();
    }

    public class MetricsDashboard : IMetricsDashboard
    {
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly Dictionary<string, double> _alertThresholds = new();
        private readonly List<Alert> _activeAlerts = new();
        private Timer _monitoringTimer;
        private const int MonitoringIntervalMs = 5000;

        public MetricsDashboard(IMetricsAggregator metricsAggregator)
        {
            _metricsAggregator = metricsAggregator;
        }

        public Task StartMonitoring(CancellationToken cancellationToken = default)
        {
            _monitoringTimer = new Timer(MonitoringCallback, null, 0, MonitoringIntervalMs);
            return Task.CompletedTask;
        }

        public Task StopMonitoring()
        {
            _monitoringTimer?.Dispose();
            return Task.CompletedTask;
        }

        public Task<DashboardData> GetDashboardData()
        {
            var currentMetrics = _metricsAggregator.GetCurrentMetrics();
            var historicalMetrics = _metricsAggregator.GetHistoricalMetrics(TimeSpan.FromHours(24));

            var dashboardData = new DashboardData
            {
                CurrentMetrics = currentMetrics,
                PerformanceTrends = CalculatePerformanceTrends(historicalMetrics),
                Bottlenecks = IdentifyBottlenecks(currentMetrics),
                ResourceUtilization = currentMetrics.ResourceMetrics,
                ActiveAlerts = _activeAlerts.ToList()
            };

            return Task.FromResult(dashboardData);
        }

        public void SetAlertThreshold(string metricName, double threshold)
        {
            _alertThresholds[metricName] = threshold;
        }

        public IEnumerable<Alert> GetActiveAlerts()
        {
            return _activeAlerts.ToList();
        }

        private void MonitoringCallback(object state)
        {
            var metrics = _metricsAggregator.GetCurrentMetrics();
            CheckAlertThresholds(metrics);
        }

        private void CheckAlertThresholds(MetricsSummary metrics)
        {
            _activeAlerts.Clear();

            foreach (var middleware in metrics.MiddlewareMetrics)
            {
                var failureRate = CalculateFailureRate(middleware.Value);
                if (_alertThresholds.TryGetValue($"{middleware.Key}_FailureRate", out var threshold)
                    && failureRate > threshold)
                {
                    _activeAlerts.Add(new Alert
                    {
                        Severity = AlertSeverity.High,
                        Message = $"High failure rate ({failureRate:P2}) detected in {middleware.Key}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            if (metrics.ResourceMetrics.CpuUsagePercent > 80)
            {
                _activeAlerts.Add(new Alert
                {
                    Severity = AlertSeverity.Medium,
                    Message = "High CPU usage detected",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private static double CalculateFailureRate(MiddlewareMetrics metrics)
        {
            var total = metrics.SuccessCount + metrics.FailureCount;
            return total == 0 ? 0 : (double)metrics.FailureCount / total;
        }

        private static PerformanceTrends CalculatePerformanceTrends(IEnumerable<MetricsSummary> historicalMetrics)
        {
            var metrics = historicalMetrics.ToList();
            return new PerformanceTrends
            {
                AverageResponseTime = metrics.SelectMany(m => 
                    m.MiddlewareMetrics.Values.Select(v => v.AverageExecutionTime)).Average(),
                SuccessRate = metrics.SelectMany(m =>
                    m.MiddlewareMetrics.Values.Select(CalculateFailureRate)).Average(),
                ResourceUtilization = new ResourceUtilizationTrend
                {
                    AverageCpuUsage = metrics.Select(m => m.ResourceMetrics.CpuUsagePercent).Average(),
                    AverageMemoryUsage = metrics.Select(m => m.ResourceMetrics.MemoryUsageMB).Average(),
                    AverageActiveThreads = metrics.Select(m => m.ResourceMetrics.ActiveThreads).Average()
                }
            };
        }

        private static List<BottleneckInfo> IdentifyBottlenecks(MetricsSummary metrics)
        {
            var bottlenecks = new List<BottleneckInfo>();

            foreach (var middleware in metrics.MiddlewareMetrics)
            {
                if (middleware.Value.AverageExecutionTime > 1000) // 1 second threshold
                {
                    bottlenecks.Add(new BottleneckInfo
                    {
                        Component = middleware.Key,
                        AverageLatency = middleware.Value.AverageExecutionTime,
                        ImpactLevel = CalculateImpactLevel(middleware.Value)
                    });
                }
            }

            return bottlenecks;
        }

        private static ImpactLevel CalculateImpactLevel(MiddlewareMetrics metrics)
        {
            var failureRate = CalculateFailureRate(metrics);
            return failureRate switch
            {
                > 0.25 => ImpactLevel.High,
                > 0.10 => ImpactLevel.Medium,
                _ => ImpactLevel.Low
            };
        }
    }

    public class DashboardData
    {
        public MetricsSummary CurrentMetrics { get; set; }
        public PerformanceTrends PerformanceTrends { get; set; }
        public List<BottleneckInfo> Bottlenecks { get; set; }
        public ResourceMetrics ResourceUtilization { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
    }

    public class PerformanceTrends
    {
        public double AverageResponseTime { get; set; }
        public double SuccessRate { get; set; }
        public ResourceUtilizationTrend ResourceUtilization { get; set; }
    }

    public class ResourceUtilizationTrend
    {
        public double AverageCpuUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public double AverageActiveThreads { get; set; }
    }

    public class BottleneckInfo
    {
        public string Component { get; set; }
        public double AverageLatency { get; set; }
        public ImpactLevel ImpactLevel { get; set; }
    }

    public class Alert
    {
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ImpactLevel
    {
        Low,
        Medium,
        High
    }
}