using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class MetricsDashboard
    {
        private readonly ValidationPipelineDashboard _pipelineDashboard;
        private readonly MetricsAggregator _metricsAggregator;
        private readonly ResourceAlertManager _alertManager;

        public MetricsDashboard(
            ValidationPipelineDashboard pipelineDashboard,
            MetricsAggregator metricsAggregator,
            ResourceAlertManager alertManager)
        {
            _pipelineDashboard = pipelineDashboard;
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
        }

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var pipelineMetrics = await _pipelineDashboard.GetRealtimeMetricsAsync();
            var resourceMetrics = await _metricsAggregator.GetResourceMetricsAsync();
            var alerts = await _alertManager.GetActiveAlertsAsync();
            var historicalData = await _pipelineDashboard.GetHistoricalAnalysisAsync(
                DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            var operationalInsights = await _pipelineDashboard.GetOperationalInsightsAsync();

            return new DashboardData
            {
                PipelineMetrics = pipelineMetrics,
                ResourceMetrics = resourceMetrics,
                ActiveAlerts = alerts,
                HistoricalAnalysis = historicalData,
                OperationalInsights = operationalInsights
            };
        }

        public async Task<ResourceMetrics> GetCurrentMetricsAsync()
        {
            return await _metricsAggregator.GetResourceMetricsAsync();
        }

        public async Task<List<Alert>> GetActiveAlertsAsync()
        {
            return await _alertManager.GetActiveAlertsAsync();
        }

        public async Task<Dictionary<string, double>> GetResourceUtilizationAsync()
        {
            return await _metricsAggregator.GetResourceUtilizationAsync();
        }

        public async Task<AlertDashboard> GetAlertDashboardAsync()
        {
            return await _pipelineDashboard.GetAlertDashboardAsync();
        }
    }

    public class DashboardData
    {
        public PipelinePerformanceMetrics PipelineMetrics { get; set; }
        public ResourceMetrics ResourceMetrics { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
        public HistoricalAnalysisReport HistoricalAnalysis { get; set; }
        public OperationalInsights OperationalInsights { get; set; }
    }
}