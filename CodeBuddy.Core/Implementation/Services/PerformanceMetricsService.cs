using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Services;

namespace CodeBuddy.Core.Implementation.Services
{
    public class PerformanceMetricsService : IPerformanceMetricsService
    {
        private readonly IPerformanceTestFramework _testFramework;
        private readonly IPerformanceTestMetricsCollector _metricsCollector;
        
        public PerformanceMetricsService(
            IPerformanceTestFramework testFramework,
            IPerformanceTestMetricsCollector metricsCollector)
        {
            _testFramework = testFramework;
            _metricsCollector = metricsCollector;
        }

        public async Task<IEnumerable<PerformanceMetrics>> GetMetricsAsync(PerformanceMetricsFilter filter)
        {
            var metrics = await _metricsCollector.GetMetricsAsync();
            return ApplyFilter(metrics, filter);
        }

        public async Task<IEnumerable<PerformanceTrend>> GetTrendsAsync(PerformanceTrendFilter filter)
        {
            var metrics = await _metricsCollector.GetHistoricalMetricsAsync(filter.StartDate, filter.EndDate);
            return CalculateTrends(metrics, filter);
        }

        public async Task<IEnumerable<PerformanceAlert>> GetActiveAlertsAsync()
        {
            var metrics = await _metricsCollector.GetLatestMetricsAsync();
            return DetectAlerts(metrics);
        }

        public async Task<PerformanceComparison> GetComparisonAsync(PerformanceComparisonFilter filter)
        {
            var baselineMetrics = await _metricsCollector.GetMetricsAsync(filter.BaselineDate);
            var currentMetrics = await _metricsCollector.GetLatestMetricsAsync();
            return CompareMetrics(baselineMetrics, currentMetrics);
        }

        public async Task<PerformanceReport> GenerateReportAsync(PerformanceMetricsFilter filter)
        {
            var metrics = await GetMetricsAsync(filter);
            return new PerformanceReport
            {
                Content = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metrics),
                GeneratedAt = DateTime.UtcNow
            };
        }

        private IEnumerable<PerformanceMetrics> ApplyFilter(IEnumerable<PerformanceMetrics> metrics, PerformanceMetricsFilter filter)
        {
            var query = metrics.AsQueryable();

            if (!string.IsNullOrEmpty(filter.Language))
                query = query.Where(m => m.Language == filter.Language);

            if (!string.IsNullOrEmpty(filter.TestType))
                query = query.Where(m => m.TestType == filter.TestType);

            if (filter.StartDate.HasValue)
                query = query.Where(m => m.Timestamp >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(m => m.Timestamp <= filter.EndDate.Value);

            return query.ToList();
        }

        private IEnumerable<PerformanceTrend> CalculateTrends(IEnumerable<PerformanceMetrics> metrics, PerformanceTrendFilter filter)
        {
            // Group metrics by relevant dimensions and calculate trends
            return metrics
                .GroupBy(m => new { m.Language, m.TestType })
                .Select(g => new PerformanceTrend
                {
                    Language = g.Key.Language,
                    TestType = g.Key.TestType,
                    CpuUtilizationTrend = CalculateMetricTrend(g.Select(m => m.CpuUtilization)),
                    MemoryUsageTrend = CalculateMetricTrend(g.Select(m => m.MemoryUsage)),
                    ExecutionTimeTrend = CalculateMetricTrend(g.Select(m => m.ExecutionTime))
                });
        }

        private double CalculateMetricTrend(IEnumerable<double> values)
        {
            // Simple linear regression slope calculation
            var n = values.Count();
            if (n < 2) return 0;

            var xValues = Enumerable.Range(0, n).Select(i => (double)i).ToList();
            var yValues = values.ToList();

            var sumX = xValues.Sum();
            var sumY = yValues.Sum();
            var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
            var sumXX = xValues.Select(x => x * x).Sum();

            return (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        }

        private IEnumerable<PerformanceAlert> DetectAlerts(IEnumerable<PerformanceMetrics> metrics)
        {
            var alerts = new List<PerformanceAlert>();
            
            foreach (var metric in metrics)
            {
                if (metric.CpuUtilization > 90)
                    alerts.Add(new PerformanceAlert 
                    { 
                        Type = "HighCpuUtilization",
                        Message = $"High CPU utilization detected for {metric.Language} {metric.TestType}: {metric.CpuUtilization}%",
                        Severity = "Warning"
                    });

                if (metric.MemoryUsage > 85)
                    alerts.Add(new PerformanceAlert
                    {
                        Type = "HighMemoryUsage",
                        Message = $"High memory usage detected for {metric.Language} {metric.TestType}: {metric.MemoryUsage}%",
                        Severity = "Warning"
                    });
            }

            return alerts;
        }

        private PerformanceComparison CompareMetrics(IEnumerable<PerformanceMetrics> baseline, IEnumerable<PerformanceMetrics> current)
        {
            return new PerformanceComparison
            {
                BaselineMetrics = baseline,
                CurrentMetrics = current,
                Differences = CalculateMetricsDifferences(baseline, current)
            };
        }

        private IEnumerable<MetricDifference> CalculateMetricsDifferences(
            IEnumerable<PerformanceMetrics> baseline,
            IEnumerable<PerformanceMetrics> current)
        {
            return current.Join(
                baseline,
                c => new { c.Language, c.TestType },
                b => new { b.Language, b.TestType },
                (c, b) => new MetricDifference
                {
                    Language = c.Language,
                    TestType = c.TestType,
                    CpuUtilizationDiff = CalculatePercentageDifference(c.CpuUtilization, b.CpuUtilization),
                    MemoryUsageDiff = CalculatePercentageDifference(c.MemoryUsage, b.MemoryUsage),
                    ExecutionTimeDiff = CalculatePercentageDifference(c.ExecutionTime, b.ExecutionTime)
                });
        }

        private double CalculatePercentageDifference(double current, double baseline)
        {
            if (baseline == 0) return 0;
            return ((current - baseline) / baseline) * 100;
        }
    }
}