using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

public interface IMetricsDashboard
{
    Task UpdateValidationMetricsAsync(ValidationMetricsUpdate metricsUpdate);
    Task UpdateResourceMetricsAsync(ResourceMetricsUpdate resourceUpdate);
    Task<DashboardMetrics> GetCurrentMetricsAsync();
    Task<DashboardMetrics> GetHistoricalMetricsAsync(DateTimeRange timeRange);
    Task<ValidationPerformanceReport> GeneratePerformanceReportAsync(DateTimeRange timeRange);
    Task<ResourceUtilizationReport> GenerateResourceReportAsync(DateTimeRange timeRange);
    Task ConfigureAlertsAsync(DashboardAlertConfig alertConfig);
    Task<DashboardState> GetDashboardStateAsync();
    Task ExportMetricsAsync(string format, DateTimeRange timeRange);
}