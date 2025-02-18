using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Models.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Logging
{
    public interface IResourceLogAggregator
    {
        Task<ResourceHealthReport> GenerateHealthReport();
        Task StartPeriodicReporting(TimeSpan interval, CancellationToken cancellationToken);
        Task<IEnumerable<LogEntry>> GetRelatedOperations(string correlationId);
        Task<ResourceUsageStatistics> GetResourceUsageStatistics(string resourceName, TimeSpan period);
    }

    public class ResourceLogAggregator : IResourceLogAggregator
    {
        private readonly IResourceLoggingService _loggingService;
        private readonly ResourceMonitoringDashboard _dashboard;

        public ResourceLogAggregator(
            IResourceLoggingService loggingService,
            ResourceMonitoringDashboard dashboard)
        {
            _loggingService = loggingService;
            _dashboard = dashboard;
        }

        public async Task<ResourceHealthReport> GenerateHealthReport()
        {
            return await _loggingService.GenerateResourceHealthReport();
        }

        public async Task StartPeriodicReporting(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var report = await GenerateHealthReport();
                    await UpdateDashboardWithReport(report);
                    await Task.Delay(interval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _loggingService.LogResourceError(
                        "PeriodicReporting",
                        "Failed to generate periodic report",
                        ex,
                        nameof(ResourceLogAggregator));
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); // Back off on error
                }
            }
        }

        public async Task<IEnumerable<LogEntry>> GetRelatedOperations(string correlationId)
        {
            return await _loggingService.GetRelatedOperations(correlationId);
        }

        public async Task<ResourceUsageStatistics> GetResourceUsageStatistics(string resourceName, TimeSpan period)
        {
            return await _loggingService.CalculateResourceUsageStatistics(resourceName, period);
        }

        private async Task UpdateDashboardWithReport(ResourceHealthReport report)
        {
            // Update real-time metrics
            foreach (var metric in report.ResourceMetrics)
            {
                await _dashboard.UpdateResourceMetrics(metric.Key, new Dictionary<string, object>
                {
                    { "CurrentAllocationCount", metric.Value.CurrentAllocationCount },
                    { "HealthScore", metric.Value.HealthScore },
                    { "ErrorCount", metric.Value.ErrorCount },
                    { "WarningCount", metric.Value.WarningCount }
                });
            }

            // Update historical patterns
            foreach (var usage in report.UsageStatistics)
            {
                await _dashboard.UpdateHistoricalUsage(usage.Key, new Dictionary<string, object>
                {
                    { "AverageUsage", usage.Value.AverageUsagePercent },
                    { "PeakUsage", usage.Value.TotalAllocations },
                    { "OrphanedCount", usage.Value.OrphanedResourceCount },
                    { "ComponentDistribution", usage.Value.ComponentUsageDistribution }
                });
            }

            // Update trends
            foreach (var trend in report.Trends)
            {
                await _dashboard.UpdateResourceTrends(trend.ResourceName, new Dictionary<string, object>
                {
                    { "TrendType", trend.TrendType },
                    { "TrendValue", trend.TrendValue },
                    { "Analysis", trend.Analysis }
                });
            }

            // Update alerts
            foreach (var alert in report.Alerts)
            {
                await _dashboard.AddResourceAlert(alert.ResourceName, new Dictionary<string, object>
                {
                    { "AlertType", alert.AlertType },
                    { "Message", alert.Message },
                    { "DetectedAt", alert.DetectedAt },
                    { "Context", alert.Context }
                });
            }

            // Trigger dashboard refresh
            await _dashboard.RefreshDisplay();
        }
    }
}