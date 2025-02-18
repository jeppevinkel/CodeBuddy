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
        Task<DashboardData> GetDashboardData(TimeRange timeRange = null);
        Task<DashboardView> GetCustomDashboardView(string viewName);
        Task SaveCustomDashboardView(DashboardView view);
        Task<ResourceMetricExport> ExportMetrics(TimeRange timeRange);
        void SetAlertThreshold(string metricName, double threshold);
        IEnumerable<Alert> GetActiveAlerts();
        Task<ResourceUsageTrends> GetResourceTrends(TimeRange timeRange);
        Task<List<ResourceOptimizationRecommendation>> GetOptimizationRecommendations();
        Task<List<ResourceBottleneck>> GetResourceBottlenecks(TimeRange timeRange);
    }

    public class MetricsDashboard : IMetricsDashboard
    {
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly Dictionary<string, double> _alertThresholds = new();
        private readonly List<Alert> _activeAlerts = new();
        private readonly Dictionary<string, DashboardView> _customViews = new();
        private readonly ResourceAnalytics _resourceAnalytics;
        private Timer _monitoringTimer;
        private const int MonitoringIntervalMs = 5000;

        public MetricsDashboard(
            IMetricsAggregator metricsAggregator,
            ResourceAnalytics resourceAnalytics)
        {
            _metricsAggregator = metricsAggregator;
            _resourceAnalytics = resourceAnalytics;
        }

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

        public async Task<DashboardData> GetDashboardData(TimeRange timeRange = null)
        {
            timeRange ??= new TimeRange 
            { 
                StartTime = DateTime.UtcNow.AddHours(-24), 
                EndTime = DateTime.UtcNow,
                Resolution = "5m"
            };

            var currentMetrics = _metricsAggregator.GetCurrentMetrics();
            var historicalMetrics = _metricsAggregator.GetHistoricalMetrics(timeRange.EndTime - timeRange.StartTime);
            var resourceTrends = await GetResourceTrends(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);
            var recommendations = await GetOptimizationRecommendations();

            var dashboardData = new DashboardData
            {
                CurrentMetrics = currentMetrics,
                PerformanceTrends = CalculatePerformanceTrends(historicalMetrics),
                Bottlenecks = bottlenecks,
                ResourceUtilization = currentMetrics.ResourceMetrics,
                ActiveAlerts = _activeAlerts.ToList(),
                ResourceTrends = resourceTrends,
                Recommendations = recommendations,
                TimeRange = timeRange
            };

            return dashboardData;
        }

        public async Task<DashboardView> GetCustomDashboardView(string viewName)
        {
            if (_customViews.TryGetValue(viewName, out var view))
            {
                await UpdateDashboardViewData(view);
                return view;
            }
            return null;
        }

        public Task SaveCustomDashboardView(DashboardView view)
        {
            _customViews[view.ViewName] = view;
            return Task.CompletedTask;
        }

        public async Task<ResourceMetricExport> ExportMetrics(TimeRange timeRange)
        {
            var resourceData = await _resourceAnalytics.GetResourceUsageData(timeRange);
            var trends = await GetResourceTrends(timeRange);
            var bottlenecks = await GetResourceBottlenecks(timeRange);
            var recommendations = await GetOptimizationRecommendations();

            return new ResourceMetricExport
            {
                ExportTimestamp = DateTime.UtcNow,
                TimeRange = timeRange,
                UsageData = resourceData,
                Bottlenecks = bottlenecks,
                Recommendations = recommendations,
                Trends = trends
            };
        }

        public async Task<ResourceUsageTrends> GetResourceTrends(TimeRange timeRange)
        {
            var trends = await _resourceAnalytics.AnalyzeResourceTrends(timeRange);
            var validatorTrends = await _resourceAnalytics.AnalyzeValidatorTrends(timeRange);
            var efficiencyMetrics = await _resourceAnalytics.CalculateEfficiencyMetrics(timeRange);

            trends.ValidatorTrends = validatorTrends;
            trends.EfficiencyMetrics = efficiencyMetrics;
            trends.AnalysisPeriod = timeRange.EndTime - timeRange.StartTime;

            return trends;
        }

        public async Task<List<ResourceOptimizationRecommendation>> GetOptimizationRecommendations()
        {
            return await _resourceAnalytics.GenerateOptimizationRecommendations();
        }

        public async Task<List<ResourceBottleneck>> GetResourceBottlenecks(TimeRange timeRange)
        {
            return await _resourceAnalytics.IdentifyBottlenecks(timeRange);
        }

        private async Task UpdateDashboardViewData(DashboardView view)
        {
            foreach (var widget in view.Widgets)
            {
                widget.Data = await GetWidgetData(widget);
            }
        }

        private async Task<Dictionary<string, object>> GetWidgetData(DashboardWidget widget)
        {
            var timeRange = ParseTimeRange(widget.Configuration);
            
            return widget.WidgetType switch
            {
                "ResourceUsage" => new Dictionary<string, object>
                {
                    ["metrics"] = await _resourceAnalytics.GetResourceUsageData(timeRange)
                },
                "Trends" => new Dictionary<string, object>
                {
                    ["trends"] = await GetResourceTrends(timeRange)
                },
                "Bottlenecks" => new Dictionary<string, object>
                {
                    ["bottlenecks"] = await GetResourceBottlenecks(timeRange)
                },
                "Recommendations" => new Dictionary<string, object>
                {
                    ["recommendations"] = await GetOptimizationRecommendations()
                },
                "Alerts" => new Dictionary<string, object>
                {
                    ["alerts"] = GetActiveAlerts()
                },
                _ => new Dictionary<string, object>()
            };
        }

        private static TimeRange ParseTimeRange(Dictionary<string, string> config)
        {
            if (!config.TryGetValue("timeRange", out var range))
                range = "24h";

            var end = DateTime.UtcNow;
            var start = range switch
            {
                "1h" => end.AddHours(-1),
                "6h" => end.AddHours(-6),
                "24h" => end.AddHours(-24),
                "7d" => end.AddDays(-7),
                "30d" => end.AddDays(-30),
                _ => end.AddHours(-24)
            };

            return new TimeRange
            {
                StartTime = start,
                EndTime = end,
                Resolution = range
            };
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
        public List<ResourceBottleneck> Bottlenecks { get; set; }
        public ResourceMetrics ResourceUtilization { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
        public ResourceUsageTrends ResourceTrends { get; set; }
        public List<ResourceOptimizationRecommendation> Recommendations { get; set; }
        public TimeRange TimeRange { get; set; }
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