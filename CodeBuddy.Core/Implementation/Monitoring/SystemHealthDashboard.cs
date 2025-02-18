using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Monitoring
{
    /// <summary>
    /// Provides a unified system-wide health monitoring dashboard that aggregates metrics
    /// from all monitoring components to provide a comprehensive view of system health.
    /// </summary>
    public class SystemHealthDashboard : IAsyncDisposable
    {
        private readonly ILogger<SystemHealthDashboard> _logger;
        private readonly MetricsDashboard _metricsDashboard;
        private readonly ResourceMonitoringDashboard _resourceDashboard;
        private readonly ValidationPipelineDashboard _pipelineDashboard;
        private readonly MemoryAnalyticsDashboard _memoryDashboard;
        private readonly PluginHealthMonitor _pluginMonitor;

        public SystemHealthDashboard(
            ILogger<SystemHealthDashboard> logger,
            MetricsDashboard metricsDashboard,
            ResourceMonitoringDashboard resourceDashboard,
            ValidationPipelineDashboard pipelineDashboard,
            MemoryAnalyticsDashboard memoryDashboard,
            PluginHealthMonitor pluginMonitor)
        {
            _logger = logger;
            _metricsDashboard = metricsDashboard;
            _resourceDashboard = resourceDashboard;
            _pipelineDashboard = pipelineDashboard;
            _memoryDashboard = memoryDashboard;
            _pluginMonitor = pluginMonitor;
        }

        public async Task<SystemHealthSnapshot> GetSystemHealthSnapshotAsync()
        {
            try
            {
                var resourceMetrics = await _resourceDashboard.GetCurrentMetricsAsync();
                var pipelineMetrics = await _pipelineDashboard.GetRealtimeMetricsAsync();
                var memoryMetrics = await _memoryDashboard.GetMemoryMetricsAsync();
                var pluginHealth = await _pluginMonitor.GetPluginHealthStatusAsync();
                var dashboardData = await _metricsDashboard.GetDashboardDataAsync();

                return new SystemHealthSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    SystemStatus = DetermineSystemStatus(resourceMetrics, pipelineMetrics, pluginHealth),
                    ResourceMetrics = new ResourceHealthMetrics
                    {
                        CpuUtilization = resourceMetrics.CpuUsagePercent,
                        MemoryUsage = resourceMetrics.MemoryUsageBytes,
                        ActiveHandles = resourceMetrics.ActiveHandles,
                        DiskUtilization = await GetDiskUtilizationAsync()
                    },
                    ValidationPipelineMetrics = new ValidationHealthMetrics
                    {
                        ActiveValidations = pipelineMetrics.SystemMetrics.TotalActiveValidations,
                        SuccessRate = pipelineMetrics.SystemMetrics.OverallSuccessRate,
                        AverageResponseTime = pipelineMetrics.SystemMetrics.AverageResponseTime,
                        QueueDepth = dashboardData.PipelineMetrics.QueueDepth
                    },
                    MemoryMetrics = new MemoryHealthMetrics
                    {
                        TotalAllocated = memoryMetrics.TotalAllocatedBytes,
                        ManagedHeapSize = memoryMetrics.ManagedHeapSizeBytes,
                        GCPressure = memoryMetrics.GCPressure,
                        LeakProbability = memoryMetrics.LeakProbability
                    },
                    PluginMetrics = new PluginHealthMetrics
                    {
                        ActivePlugins = pluginHealth.ActivePlugins,
                        FailedPlugins = pluginHealth.FailedPlugins,
                        TotalPlugins = pluginHealth.TotalPlugins,
                        PluginErrors = pluginHealth.RecentErrors
                    },
                    CacheMetrics = new CacheHealthMetrics
                    {
                        HitRate = dashboardData.OperationalInsights.ConfigurationEffectiveness.CacheHitRate,
                        MissRate = dashboardData.OperationalInsights.ConfigurationEffectiveness.CacheMissRate,
                        EvictionRate = dashboardData.OperationalInsights.ConfigurationEffectiveness.CacheEvictionRate,
                        TotalEntries = dashboardData.OperationalInsights.ConfigurationEffectiveness.CacheTotalEntries
                    },
                    Alerts = await GetConsolidatedAlertsAsync(),
                    PerformanceIndicators = await GetPerformanceIndicatorsAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system health snapshot");
                throw;
            }
        }

        public async Task<SystemHealthHistory> GetHistoricalHealthDataAsync(DateTime startTime, DateTime endTime)
        {
            var pipelineHistory = await _pipelineDashboard.GetHistoricalAnalysisAsync(startTime, endTime);
            var resourceTrends = _resourceDashboard.GetTrendData(endTime - startTime);
            
            return new SystemHealthHistory
            {
                TimeRange = new TimeRange { Start = startTime, End = endTime },
                ResourceTrends = resourceTrends,
                PipelinePerformance = pipelineHistory.PerformanceTrends,
                MemoryTrends = await _memoryDashboard.GetMemoryTrendsAsync(startTime, endTime),
                PluginHealth = await _pluginMonitor.GetPluginHealthHistoryAsync(startTime, endTime),
                Alerts = await _pipelineDashboard.GetAlertDashboardAsync(),
                Insights = await _pipelineDashboard.GetOperationalInsightsAsync()
            };
        }

        private SystemStatus DetermineSystemStatus(
            ResourceMetricsModel resourceMetrics,
            ValidationDashboardSummary pipelineMetrics,
            PluginHealthStatus pluginHealth)
        {
            if (HasCriticalIssues(resourceMetrics, pipelineMetrics, pluginHealth))
                return SystemStatus.Critical;
            
            if (HasWarnings(resourceMetrics, pipelineMetrics, pluginHealth))
                return SystemStatus.Warning;
            
            return SystemStatus.Healthy;
        }

        private bool HasCriticalIssues(
            ResourceMetricsModel resourceMetrics,
            ValidationDashboardSummary pipelineMetrics,
            PluginHealthStatus pluginHealth)
        {
            return resourceMetrics.CpuUsagePercent >= 90 ||
                   resourceMetrics.MemoryUsageBytes >= 90 * 1024 * 1024 * 1024 || // 90% of system memory
                   pipelineMetrics.SystemMetrics.OverallSuccessRate <= 0.8 ||
                   pluginHealth.FailedPlugins >= pluginHealth.TotalPlugins * 0.2; // 20% plugin failure
        }

        private bool HasWarnings(
            ResourceMetricsModel resourceMetrics,
            ValidationDashboardSummary pipelineMetrics,
            PluginHealthStatus pluginHealth)
        {
            return resourceMetrics.CpuUsagePercent >= 70 ||
                   resourceMetrics.MemoryUsageBytes >= 70 * 1024 * 1024 * 1024 || // 70% of system memory
                   pipelineMetrics.SystemMetrics.OverallSuccessRate <= 0.9 ||
                   pluginHealth.FailedPlugins >= pluginHealth.TotalPlugins * 0.1; // 10% plugin failure
        }

        private async Task<double> GetDiskUtilizationAsync()
        {
            try
            {
                var driveInfo = new System.IO.DriveInfo(System.IO.Directory.GetCurrentDirectory());
                return 100.0 * (driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting disk utilization");
                return 0;
            }
        }

        private async Task<List<SystemAlert>> GetConsolidatedAlertsAsync()
        {
            var alerts = new List<SystemAlert>();
            
            // Aggregate alerts from all monitoring components
            alerts.AddRange((await _resourceDashboard.GetActiveAlerts())
                .Select(a => new SystemAlert
                {
                    Source = "Resource",
                    Severity = a.Severity,
                    Message = a.Message,
                    Timestamp = a.Timestamp,
                    Details = a.CurrentValues
                }));

            alerts.AddRange((await _pipelineDashboard.GetAlertDashboardAsync()).CurrentAlerts
                .Select(a => new SystemAlert
                {
                    Source = "Pipeline",
                    Severity = a.Severity,
                    Message = a.Message,
                    Timestamp = a.Timestamp,
                    Details = a.Metrics
                }));

            return alerts;
        }

        private async Task<Dictionary<string, double>> GetPerformanceIndicatorsAsync()
        {
            return new Dictionary<string, double>
            {
                ["SystemLoad"] = await GetSystemLoadAsync(),
                ["ResponseTime"] = await GetAverageResponseTimeAsync(),
                ["ErrorRate"] = await GetSystemErrorRateAsync(),
                ["ResourceEfficiency"] = await GetResourceEfficiencyScoreAsync()
            };
        }

        private async Task<double> GetSystemLoadAsync()
        {
            var resourceMetrics = await _resourceDashboard.GetCurrentMetricsAsync();
            return (resourceMetrics.CpuUsagePercent + 
                   (resourceMetrics.MemoryUsageBytes / (double)(8 * 1024 * 1024 * 1024) * 100)) / 2;
        }

        private async Task<double> GetAverageResponseTimeAsync()
        {
            var pipelineMetrics = await _pipelineDashboard.GetRealtimeMetricsAsync();
            return pipelineMetrics.SystemMetrics.AverageResponseTime;
        }

        private async Task<double> GetSystemErrorRateAsync()
        {
            var pipelineMetrics = await _pipelineDashboard.GetRealtimeMetricsAsync();
            return 100 * (1 - pipelineMetrics.SystemMetrics.OverallSuccessRate);
        }

        private async Task<double> GetResourceEfficiencyScoreAsync()
        {
            var resourceMetrics = await _resourceDashboard.GetCurrentMetricsAsync();
            var pipelineMetrics = await _pipelineDashboard.GetRealtimeMetricsAsync();
            
            double cpuEfficiency = 1 - (resourceMetrics.CpuUsagePercent / 100);
            double memoryEfficiency = 1 - (resourceMetrics.MemoryUsageBytes / (8.0 * 1024 * 1024 * 1024));
            double throughputEfficiency = pipelineMetrics.SystemMetrics.OverallSuccessRate;
            
            return (cpuEfficiency + memoryEfficiency + throughputEfficiency) / 3 * 100;
        }

        public async ValueTask DisposeAsync()
        {
            if (_resourceDashboard != null)
            {
                await _resourceDashboard.DisposeAsync();
            }
        }
    }

    public class SystemHealthSnapshot
    {
        public DateTime Timestamp { get; set; }
        public SystemStatus Status { get; set; }
        public ResourceHealthMetrics ResourceMetrics { get; set; }
        public ValidationHealthMetrics ValidationPipelineMetrics { get; set; }
        public MemoryHealthMetrics MemoryMetrics { get; set; }
        public PluginHealthMetrics PluginMetrics { get; set; }
        public CacheHealthMetrics CacheMetrics { get; set; }
        public List<SystemAlert> Alerts { get; set; }
        public Dictionary<string, double> PerformanceIndicators { get; set; }
    }

    public class ResourceHealthMetrics
    {
        public double CpuUtilization { get; set; }
        public long MemoryUsage { get; set; }
        public int ActiveHandles { get; set; }
        public double DiskUtilization { get; set; }
    }

    public class ValidationHealthMetrics
    {
        public int ActiveValidations { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
        public int QueueDepth { get; set; }
    }

    public class MemoryHealthMetrics
    {
        public long TotalAllocated { get; set; }
        public long ManagedHeapSize { get; set; }
        public double GCPressure { get; set; }
        public double LeakProbability { get; set; }
    }

    public class PluginHealthMetrics
    {
        public int ActivePlugins { get; set; }
        public int FailedPlugins { get; set; }
        public int TotalPlugins { get; set; }
        public List<string> PluginErrors { get; set; }
    }

    public class CacheHealthMetrics
    {
        public double HitRate { get; set; }
        public double MissRate { get; set; }
        public double EvictionRate { get; set; }
        public int TotalEntries { get; set; }
    }

    public class SystemHealthHistory
    {
        public TimeRange TimeRange { get; set; }
        public ResourceTrendData ResourceTrends { get; set; }
        public List<TimeSeriesMetric> PipelinePerformance { get; set; }
        public List<MemoryTrendData> MemoryTrends { get; set; }
        public List<PluginHealthSnapshot> PluginHealth { get; set; }
        public AlertDashboard Alerts { get; set; }
        public OperationalInsights Insights { get; set; }
    }

    public class SystemAlert
    {
        public string Source { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Details { get; set; }
    }

    public enum SystemStatus
    {
        Healthy,
        Warning,
        Critical
    }
}