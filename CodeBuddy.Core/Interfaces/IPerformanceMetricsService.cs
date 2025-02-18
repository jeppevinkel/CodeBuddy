using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Services
{
    public interface IPerformanceMetricsService
    {
        Task<IEnumerable<PerformanceMetrics>> GetMetricsAsync(PerformanceMetricsFilter filter);
        Task<IEnumerable<PerformanceTrend>> GetTrendsAsync(PerformanceTrendFilter filter);
        Task<IEnumerable<PerformanceAlert>> GetActiveAlertsAsync();
        Task<PerformanceComparison> GetComparisonAsync(PerformanceComparisonFilter filter);
        Task<PerformanceReport> GenerateReportAsync(PerformanceMetricsFilter filter);
    }
}