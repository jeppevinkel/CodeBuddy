using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics;

public interface ITimeSeriesStorage
{
    Task StoreMetricsAsync(MetricsDataPoint dataPoint);
    Task<IEnumerable<MetricsDataPoint>> QueryMetricsAsync(string metricName, DateTime startTime, DateTime endTime, IDictionary<string, string> tags = null);
    Task<IEnumerable<MetricsDataPoint>> QueryMetricsAggregateAsync(string metricName, DateTime startTime, DateTime endTime, string aggregation, TimeSpan interval);
    Task<IEnumerable<TimeSeriesPattern>> AnalyzePatternsAsync(string metricName, DateTime startTime, DateTime endTime);
    Task<IEnumerable<TimeSeriesAnomaly>> DetectAnomaliesAsync(string metricName, DateTime startTime, DateTime endTime);
    Task<IDictionary<string, double>> ComputeMetricStatisticsAsync(string metricName, DateTime startTime, DateTime endTime);
    Task DeleteMetricsAsync(string metricName, DateTime startTime, DateTime endTime, IDictionary<string, string> tags = null);
    Task ConfigureRetentionPolicyAsync(string metricName, TimeSpan retentionPeriod);
}