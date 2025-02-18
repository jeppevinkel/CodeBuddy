using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsDashboard
    {
        Task<DashboardData> GetDashboardDataAsync(DashboardOptions options);
    }

    public class MetricsDashboard : IMetricsDashboard
    {
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly IResourceAlertManager _alertManager;

        public MetricsDashboard(
            IResourceAnalytics resourceAnalytics,
            IMetricsAggregator metricsAggregator,
            IResourceAlertManager alertManager)
        {
            _resourceAnalytics = resourceAnalytics;
            _metricsAggregator = metricsAggregator;
            _alertManager = alertManager;
        }

        public async Task<DashboardData> GetDashboardDataAsync(DashboardOptions options)
        {
            var historicalData = await _resourceAnalytics.GetHistoricalDataAsync(
                options.StartTime,
                options.EndTime,
                options.ResourceTypes);

            var trends = await _resourceAnalytics.AnalyzeResourceTrendsAsync(
                options.ResourceTypes,
                options.TrendAnalysisPeriod);

            var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync(new AnalysisOptions
            {
                IncludeHistoricalTrends = true,
                LookbackPeriod = options.TrendAnalysisPeriod,
                AnalysisGranularity = options.TimeGranularity,
                ResourceTypes = options.ResourceTypes
            });

            var realtimeMetrics = _metricsAggregator.GetCurrentMetrics();
            var alertHistory = await _alertManager.GetAlertHistoryAsync(options.StartTime, options.EndTime);
            var thresholds = await _alertManager.GetResourceThresholdsAsync();

            return new DashboardData
            {
                HistoricalData = historicalData,
                ResourceTrends = trends,
                Bottlenecks = bottlenecks,
                RealtimeMetrics = realtimeMetrics,
                TimeRange = new TimeRange
                {
                    Start = options.StartTime,
                    End = options.EndTime,
                    Granularity = options.TimeGranularity
                },
                ResourceThresholds = thresholds,
                AlertHistory = alertHistory,
                Predictions = await GenerateResourcePredictions(trends),
                PerformanceMetrics = await GetPerformanceMetrics(options),
                ResourceUtilization = CalculateResourceUtilization(realtimeMetrics),
                OptimizationRecommendations = GenerateOptimizationRecommendations(bottlenecks, trends)
            };
        }

        private async Task<List<ResourcePrediction>> GenerateResourcePredictions(List<ResourceTrend> trends)
        {
            var predictions = new List<ResourcePrediction>();
            foreach (var trend in trends)
            {
                predictions.Add(new ResourcePrediction
                {
                    ResourceType = trend.ResourceType,
                    PredictedValue = trend.ProjectedValue,
                    PredictionTime = DateTime.UtcNow.AddHours(1),
                    Confidence = trend.TrendConfidence,
                    ThresholdCrossingTime = trend.ProjectedThresholdCrossing
                });
            }
            return predictions;
        }

        private async Task<PerformanceMetrics> GetPerformanceMetrics(DashboardOptions options)
        {
            return new PerformanceMetrics
            {
                AverageResponseTime = await _metricsAggregator.GetAverageResponseTimeAsync(options.TimeRange),
                Throughput = await _metricsAggregator.GetThroughputAsync(options.TimeRange),
                ErrorRate = await _metricsAggregator.GetErrorRateAsync(options.TimeRange),
                ConcurrentOperations = await _metricsAggregator.GetConcurrentOperationsAsync(),
                ResourceEfficiency = await _metricsAggregator.GetResourceEfficiencyAsync(options.TimeRange)
            };
        }

        private ResourceUtilization CalculateResourceUtilization(RealtimeMetrics metrics)
        {
            return new ResourceUtilization
            {
                CpuUtilization = new UtilizationMetric
                {
                    Current = metrics.CpuUsagePercent,
                    Peak = metrics.PeakCpuUsage,
                    Average = metrics.AverageCpuUsage,
                    Trend = metrics.CpuUsageTrend
                },
                MemoryUtilization = new UtilizationMetric
                {
                    Current = metrics.MemoryUsagePercent,
                    Peak = metrics.PeakMemoryUsage,
                    Average = metrics.AverageMemoryUsage,
                    Trend = metrics.MemoryUsageTrend
                },
                DiskUtilization = new UtilizationMetric
                {
                    Current = metrics.DiskUtilizationPercent,
                    Peak = metrics.PeakDiskUtilization,
                    Average = metrics.AverageDiskUtilization,
                    Trend = metrics.DiskUtilizationTrend
                },
                NetworkUtilization = new UtilizationMetric
                {
                    Current = metrics.NetworkUtilizationPercent,
                    Peak = metrics.PeakNetworkUtilization,
                    Average = metrics.AverageNetworkUtilization,
                    Trend = metrics.NetworkUtilizationTrend
                }
            };
        }

        private List<OptimizationRecommendation> GenerateOptimizationRecommendations(
            List<ResourceBottleneck> bottlenecks,
            List<ResourceTrend> trends)
        {
            var recommendations = new List<OptimizationRecommendation>();

            foreach (var bottleneck in bottlenecks)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    ResourceType = bottleneck.ResourceType,
                    Priority = bottleneck.Severity,
                    Impact = bottleneck.Impact,
                    RecommendedAction = bottleneck.RecommendedAction,
                    ExpectedImprovement = bottleneck.ProjectedImprovement,
                    Implementation = bottleneck.MitigationSteps,
                    TimeToImplement = bottleneck.EstimatedResolutionTime
                });
            }

            foreach (var trend in trends.Where(t => t.TrendDirection == TrendDirection.Increasing))
            {
                if (trend.ProjectedThresholdCrossing.HasValue &&
                    trend.ProjectedThresholdCrossing.Value < DateTime.UtcNow.AddDays(7))
                {
                    recommendations.Add(new OptimizationRecommendation
                    {
                        ResourceType = trend.ResourceType,
                        Priority = AlertSeverity.Warning,
                        Impact = "Potential resource exhaustion based on current trends",
                        RecommendedAction = $"Consider scaling {trend.ResourceType} resources",
                        ExpectedImprovement = "Prevent resource bottleneck",
                        Implementation = new List<string>
                        {
                            $"Monitor {trend.ResourceType} usage patterns",
                            "Evaluate scaling options",
                            "Implement resource optimization",
                            "Update alert thresholds"
                        },
                        TimeToImplement = TimeSpan.FromDays(2)
                    });
                }
            }

            return recommendations;
        }
    }
}