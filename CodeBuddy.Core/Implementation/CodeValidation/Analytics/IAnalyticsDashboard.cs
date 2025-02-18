using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics;

public interface IAnalyticsDashboard
{
    Task ProcessValidationDataAsync(ValidationAnalyticsData analyticsData);
    Task<IEnumerable<ResourceUsageTrend>> AnalyzeResourceTrendsAsync(DateTimeRange timeRange);
    Task<IEnumerable<PerformanceAnomaly>> DetectPerformanceAnomaliesAsync(DateTimeRange timeRange);
    Task<ResourceOptimizationReport> GenerateOptimizationRecommendationsAsync();
    Task<IEnumerable<ResourceBottleneck>> IdentifyBottlenecksAsync(DateTimeRange timeRange);
    Task<CapacityPlanningReport> GenerateCapacityPlanningReportAsync(DateTimeRange timeRange);
    Task<IEnumerable<ValidationPatternInsight>> AnalyzeValidationPatternsAsync(DateTimeRange timeRange);
    Task<SLAComplianceReport> GenerateSLAComplianceReportAsync(DateTimeRange timeRange);
    Task<PerformanceForecast> GeneratePerformanceForecastAsync(DateTimeRange forecastRange);
    Task ConfigureAnalyticsPipelinesAsync(AnalyticsPipelineConfig config);
    Task<AnalyticsDashboardState> GetDashboardStateAsync();
    Task ExportAnalyticsReportAsync(string reportType, DateTimeRange timeRange, string format);
}