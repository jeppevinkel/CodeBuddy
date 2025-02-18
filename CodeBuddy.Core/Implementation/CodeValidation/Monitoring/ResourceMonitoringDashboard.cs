using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Logging;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ResourceMonitoringDashboard
    {
        private readonly IResourceLoggingService _loggingService;
        private readonly IResourceLogAggregator _logAggregator;
        private readonly Dictionary<string, Dictionary<string, object>> _resourceMetrics;
        private readonly object _metricsLock = new object();

        public ResourceMonitoringDashboard(
            IResourceLoggingService loggingService,
            IResourceLogAggregator logAggregator)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _logAggregator = logAggregator ?? throw new ArgumentNullException(nameof(logAggregator));
            _resourceMetrics = new Dictionary<string, Dictionary<string, object>>();
        }

        public async Task UpdateResourceMetrics(string resourceId, Dictionary<string, object> metrics)
        {
            lock (_metricsLock)
            {
                if (!_resourceMetrics.ContainsKey(resourceId))
                {
                    _resourceMetrics[resourceId] = new Dictionary<string, object>();
                }

                foreach (var metric in metrics)
                {
                    _resourceMetrics[resourceId][metric.Key] = metric.Value;
                }
            }

            _loggingService.LogResourceAllocation(
                resourceId,
                "MetricsUpdate",
                metrics,
                nameof(ResourceMonitoringDashboard));
        }

        public async Task UpdateResourceOperation(string resourceId, string operation, Dictionary<string, object> details)
        {
            var metrics = new Dictionary<string, object>(details)
            {
                { "Operation", operation },
                { "Timestamp", DateTime.UtcNow }
            };

            _loggingService.LogResourceAllocation(
                resourceId,
                operation,
                metrics,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics(resourceId, metrics);
        }

        public async Task UpdateHistoricalUsage(string resourceId, Dictionary<string, object> usageData)
        {
            _loggingService.LogResourceAllocation(
                resourceId,
                "HistoricalUsageUpdate",
                usageData,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics(resourceId, usageData);
        }

        public async Task UpdateResourceTrends(string resourceId, Dictionary<string, object> trends)
        {
            _loggingService.LogResourceAllocation(
                resourceId,
                "TrendUpdate",
                trends,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics(resourceId, trends);
        }

        public async Task AddResourceAlert(string resourceId, Dictionary<string, object> alertInfo)
        {
            _loggingService.LogResourceWarning(
                resourceId,
                alertInfo["Message"].ToString(),
                alertInfo,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics(resourceId, alertInfo);
        }

        public async Task UpdateLeakDetectionMetrics(Dictionary<string, object> metrics)
        {
            _loggingService.LogResourceAllocation(
                "LeakDetection",
                "MetricsUpdate",
                metrics,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics("LeakDetection", metrics);
        }

        public async Task UpdateEmergencyCleanupMetrics(Dictionary<string, object> metrics)
        {
            _loggingService.LogResourceAllocation(
                "EmergencyCleanup",
                "MetricsUpdate",
                metrics,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics("EmergencyCleanup", metrics);
        }

        public async Task UpdateMetrics(Dictionary<string, object> metrics)
        {
            _loggingService.LogResourceAllocation(
                "Dashboard",
                "MetricsUpdate",
                metrics,
                nameof(ResourceMonitoringDashboard));

            await UpdateResourceMetrics("General", metrics);
        }

        public async Task RefreshDisplay()
        {
            var report = await _logAggregator.GenerateHealthReport();
            
            foreach (var metric in report.ResourceMetrics)
            {
                await UpdateResourceMetrics(metric.Key, new Dictionary<string, object>
                {
                    { "HealthScore", metric.Value.HealthScore },
                    { "CurrentAllocationCount", metric.Value.CurrentAllocationCount },
                    { "ErrorCount", metric.Value.ErrorCount },
                    { "WarningCount", metric.Value.WarningCount }
                });
            }

            foreach (var trend in report.Trends)
            {
                await UpdateResourceTrends(trend.ResourceName, trend.TrendMetrics);
            }

            foreach (var alert in report.Alerts)
            {
                await AddResourceAlert(alert.ResourceName, alert.Context);
            }
        }

        public Dictionary<string, Dictionary<string, object>> GetCurrentMetrics()
        {
            lock (_metricsLock)
            {
                return new Dictionary<string, Dictionary<string, object>>(_resourceMetrics);
            }
        }
    }
}