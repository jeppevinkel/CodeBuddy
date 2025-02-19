using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public interface IResourceAnalytics
    {
        Task StoreResourceUsageDataAsync(ResourceUsageData data);
        Task<ResourceUsageReport> GenerateReportAsync(TimeSpan period);
        Task<IEnumerable<ResourceOptimizationRecommendation>> GetOptimizationRecommendationsAsync();
        Task<ResourceUsageTrends> AnalyzeUsageTrendsAsync();
        Task<IEnumerable<ResourceBottleneck>> IdentifyBottlenecksAsync();
    }

    public class ResourceAnalytics : IResourceAnalytics
    {
        private readonly ITimeSeriesStorage _timeSeriesStorage;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly IResourceAlertManager _resourceAlertManager;

        public ResourceAnalytics(
            ITimeSeriesStorage timeSeriesStorage,
            IMetricsAggregator metricsAggregator,
            IResourceAlertManager resourceAlertManager)
        {
            _timeSeriesStorage = timeSeriesStorage;
            _metricsAggregator = metricsAggregator;
            _resourceAlertManager = resourceAlertManager;
        }

        public async Task StoreResourceUsageDataAsync(ResourceUsageData data)
        {
            await _timeSeriesStorage.StoreDataPointAsync(new TimeSeriesDataPoint
            {
                Timestamp = DateTime.UtcNow,
                Metrics = new Dictionary<string, double>
                {
                    { "CpuUsage", data.CpuUsagePercentage },
                    { "MemoryUsage", data.MemoryUsageMB },
                    { "DiskIORate", data.DiskIOBytesPerSecond },
                    { "Gen0Size", data.Gen0SizeBytes },
                    { "Gen1Size", data.Gen1SizeBytes },
                    { "Gen2Size", data.Gen2SizeBytes },
                    { "LohSize", data.LohSizeBytes },
                    { "FinalizationQueueLength", data.FinalizationQueueLength },
                    { "MemoryFragmentation", data.FragmentationPercent }
                },
                Tags = new Dictionary<string, string>
                {
                    { "PipelineId", data.PipelineId },
                    { "ValidatorType", data.ValidatorType }
                }
            });
        }

        public async Task<ResourceUsageReport> GenerateReportAsync(TimeSpan period)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - period;

            var timeSeriesData = await _timeSeriesStorage.GetDataPointsAsync(startTime, endTime);
            var aggregatedMetrics = await _metricsAggregator.AggregateMetricsAsync(timeSeriesData);
            var throttlingEvents = await _resourceAlertManager.GetThrottlingEventsAsync(startTime, endTime);

            return new ResourceUsageReport
            {
                Period = period,
                StartTime = startTime,
                EndTime = endTime,
                AverageMetrics = aggregatedMetrics,
                ThrottlingEvents = throttlingEvents,
                ResourceUtilization = CalculateResourceUtilization(timeSeriesData),
                PerformanceMetrics = await CalculatePerformanceMetrics(timeSeriesData)
            };
        }

        public async Task<IEnumerable<ResourceOptimizationRecommendation>> GetOptimizationRecommendationsAsync()
        {
            var recommendations = new List<ResourceOptimizationRecommendation>();
            var recentData = await _timeSeriesStorage.GetDataPointsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            
            var usagePatterns = AnalyzeUsagePatterns(recentData);
            foreach (var pattern in usagePatterns)
            {
                if (pattern.IndicatesInefficiency)
                {
                    recommendations.Add(new ResourceOptimizationRecommendation
                    {
                        ResourceType = pattern.ResourceType,
                        CurrentUsage = pattern.CurrentUsage,
                        RecommendedUsage = pattern.OptimalUsage,
                        Impact = pattern.PotentialImpact,
                        Justification = pattern.Analysis
                    });
                }
            }

            return recommendations;
        }

        public async Task<ResourceUsageTrends> AnalyzeUsageTrendsAsync()
        {
            var monthlyData = await _timeSeriesStorage.GetDataPointsAsync(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            
            return new ResourceUsageTrends
            {
                CpuTrend = CalculateTrend(monthlyData.Select(d => d.Metrics["CpuUsage"])),
                MemoryTrend = CalculateTrend(monthlyData.Select(d => d.Metrics["MemoryUsage"])),
                DiskIOTrend = CalculateTrend(monthlyData.Select(d => d.Metrics["DiskIORate"])),
                PredictedUsage = PredictFutureUsage(monthlyData)
            };
        }

        public async Task<IEnumerable<ResourceBottleneck>> IdentifyBottlenecksAsync()
        {
            var bottlenecks = new List<ResourceBottleneck>();
            var recentData = await _timeSeriesStorage.GetDataPointsAsync(DateTime.UtcNow.AddHours(-24), DateTime.UtcNow);
            
            // Analyze CPU bottlenecks
            var cpuBottlenecks = AnalyzeResourceBottlenecks(recentData, "CpuUsage", 90.0);
            bottlenecks.AddRange(cpuBottlenecks);

            // Analyze Memory bottlenecks
            var memoryBottlenecks = AnalyzeResourceBottlenecks(recentData, "MemoryUsage", 85.0);
            bottlenecks.AddRange(memoryBottlenecks);

            // Analyze Disk I/O bottlenecks
            var diskBottlenecks = AnalyzeResourceBottlenecks(recentData, "DiskIORate", 80.0);
            bottlenecks.AddRange(diskBottlenecks);

            return bottlenecks;
        }

        private ResourceUtilization CalculateResourceUtilization(IEnumerable<TimeSeriesDataPoint> data)
        {
            var latest = data.OrderByDescending(d => d.Timestamp).FirstOrDefault();
            if (latest == null) return new ResourceUtilization();

            return new ResourceUtilization
            {
                CpuUtilization = latest.Metrics["CpuUsage"],
                MemoryUtilization = latest.Metrics["MemoryUsage"],
                DiskUtilization = latest.Metrics["DiskIORate"],
                NetworkUtilization = 0 // To be implemented
            };
        }

        private async Task<PerformanceMetrics> CalculatePerformanceMetrics(IEnumerable<TimeSeriesDataPoint> data)
        {
            var validatorPerf = await CalculateValidatorPerformance(data);
            var cacheMetrics = await _metricsAggregator.GetCacheMetricsAsync();
            var efficiency = CalculateEfficiency(data);

            return new PerformanceMetrics
            {
                ValidatorPerformance = validatorPerf,
                CacheMetrics = cacheMetrics,
                ResourceEfficiency = efficiency
            };
        }

        private IEnumerable<UsagePattern> AnalyzeUsagePatterns(IEnumerable<TimeSeriesDataPoint> data)
        {
            var patterns = new List<UsagePattern>();
            var groupedData = data.GroupBy(d => d.Timestamp.Hour);

            foreach (var hourGroup in groupedData)
            {
                var cpuPattern = AnalyzeMetricPattern(hourGroup, "CpuUsage");
                var memoryPattern = AnalyzeMetricPattern(hourGroup, "MemoryUsage");
                var diskPattern = AnalyzeMetricPattern(hourGroup, "DiskIORate");

                patterns.AddRange(new[] { cpuPattern, memoryPattern, diskPattern });
            }

            return patterns;
        }

        private TrendAnalysis CalculateTrend(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (!valuesList.Any())
                return new TrendAnalysis { Trend = "Stable", GrowthRate = 0 };

            var growthRate = (valuesList.Last() - valuesList.First()) / valuesList.Count;
            
            return new TrendAnalysis
            {
                Trend = growthRate switch
                {
                    var rate when rate > 0.1 => "Increasing",
                    var rate when rate < -0.1 => "Decreasing",
                    _ => "Stable"
                },
                GrowthRate = growthRate
            };
        }

        private PredictedResourceUsage PredictFutureUsage(IEnumerable<TimeSeriesDataPoint> historicalData)
        {
            var data = historicalData.OrderBy(d => d.Timestamp).ToList();
            if (data.Count < 2) 
                return new PredictedResourceUsage();

            // Simple linear regression for prediction
            var cpuTrend = CalculateLinearTrend(data.Select(d => d.Metrics["CpuUsage"]));
            var memoryTrend = CalculateLinearTrend(data.Select(d => d.Metrics["MemoryUsage"]));
            var diskTrend = CalculateLinearTrend(data.Select(d => d.Metrics["DiskIORate"]));

            return new PredictedResourceUsage
            {
                PredictedCpuUsage = cpuTrend.Predict(data.Count + 1),
                PredictedMemoryUsage = memoryTrend.Predict(data.Count + 1),
                PredictedDiskIO = diskTrend.Predict(data.Count + 1),
                Confidence = CalculatePredictionConfidence(data)
            };
        }

        private IEnumerable<ResourceBottleneck> AnalyzeResourceBottlenecks(
            IEnumerable<TimeSeriesDataPoint> data,
            string metricName,
            double threshold)
        {
            var bottlenecks = new List<ResourceBottleneck>();
            var timeSeriesData = data.OrderBy(d => d.Timestamp).ToList();

            for (int i = 0; i < timeSeriesData.Count; i++)
            {
                if (timeSeriesData[i].Metrics[metricName] > threshold)
                {
                    var duration = DetectBottleneckDuration(timeSeriesData, i, metricName, threshold);
                    bottlenecks.Add(new ResourceBottleneck
                    {
                        ResourceType = metricName,
                        StartTime = timeSeriesData[i].Timestamp,
                        Duration = duration,
                        Severity = CalculateBottleneckSeverity(timeSeriesData[i].Metrics[metricName], threshold),
                        Impact = CalculateBottleneckImpact(timeSeriesData, i, metricName)
                    });
                    i += duration.Minutes; // Skip the bottleneck period
                }
            }

            return bottlenecks;
        }

        private TimeSpan DetectBottleneckDuration(List<TimeSeriesDataPoint> data, int startIndex, string metricName, double threshold)
        {
            var start = data[startIndex].Timestamp;
            var endIndex = startIndex;

            while (endIndex < data.Count && data[endIndex].Metrics[metricName] > threshold)
            {
                endIndex++;
            }

            return endIndex < data.Count 
                ? data[endIndex].Timestamp - start 
                : TimeSpan.FromMinutes(5); // Default duration if bottleneck continues
        }

        private string CalculateBottleneckSeverity(double value, double threshold)
        {
            var ratio = value / threshold;
            return ratio switch
            {
                var r when r > 1.5 => "Critical",
                var r when r > 1.2 => "High",
                _ => "Moderate"
            };
        }

        private double CalculateBottleneckImpact(List<TimeSeriesDataPoint> data, int bottleneckIndex, string metricName)
        {
            // Calculate impact based on performance degradation during bottleneck
            var normalPerformance = data.Take(bottleneckIndex)
                .Average(d => d.Metrics[metricName]);
            var bottleneckPerformance = data[bottleneckIndex].Metrics[metricName];

            return (bottleneckPerformance - normalPerformance) / normalPerformance * 100;
        }

        private LinearTrend CalculateLinearTrend(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            var n = valuesList.Count;
            var xValues = Enumerable.Range(0, n).Select(i => (double)i).ToList();

            var sumX = xValues.Sum();
            var sumY = valuesList.Sum();
            var sumXY = xValues.Zip(valuesList, (x, y) => x * y).Sum();
            var sumX2 = xValues.Sum(x => x * x);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            return new LinearTrend { Slope = slope, Intercept = intercept };
        }

        private double CalculatePredictionConfidence(List<TimeSeriesDataPoint> data)
        {
            // Simplified confidence calculation based on data consistency
            var variations = data.Zip(data.Skip(1), (a, b) => 
                Math.Abs(a.Metrics["CpuUsage"] - b.Metrics["CpuUsage"]) +
                Math.Abs(a.Metrics["MemoryUsage"] - b.Metrics["MemoryUsage"]) +
                Math.Abs(a.Metrics["DiskIORate"] - b.Metrics["DiskIORate"])
            ).Average();

            return Math.Max(0, 100 - variations);
        }

        private async Task<Dictionary<string, double>> CalculateValidatorPerformance(IEnumerable<TimeSeriesDataPoint> data)
        {
            var latest = data.OrderByDescending(d => d.Timestamp).FirstOrDefault();
            if (latest == null) return new Dictionary<string, double>();

            return new Dictionary<string, double>
            {
                { "ThroughputPerSecond", latest.Metrics.GetValueOrDefault("ValidatorThroughput", 0) },
                { "AverageLatencyMs", latest.Metrics.GetValueOrDefault("ValidatorLatency", 0) },
                { "ErrorRate", latest.Metrics.GetValueOrDefault("ValidatorErrorRate", 0) },
                { "QueueLength", latest.Metrics.GetValueOrDefault("ValidatorQueueLength", 0) }
            };
        }

        private ResourceEfficiency CalculateEfficiency(IEnumerable<TimeSeriesDataPoint> data)
        {
            var latest = data.OrderByDescending(d => d.Timestamp).FirstOrDefault();
            if (latest == null) return new ResourceEfficiency();

            var componentEfficiency = new Dictionary<string, double>
            {
                { "CPU", CalculateComponentEfficiency(latest.Metrics["CpuUsage"]) },
                { "Memory", CalculateComponentEfficiency(latest.Metrics["MemoryUsage"]) },
                { "DiskIO", CalculateComponentEfficiency(latest.Metrics["DiskIORate"]) }
            };

            return new ResourceEfficiency
            {
                ResourceUtilizationScore = componentEfficiency.Values.Average(),
                ResourceWasteScore = 100 - componentEfficiency.Values.Average(),
                ComponentEfficiency = componentEfficiency,
                OptimizationSuggestions = GenerateOptimizationSuggestions(componentEfficiency)
            };
        }

        private double CalculateComponentEfficiency(double utilization)
        {
            // Optimal utilization is considered to be between 60% and 80%
            if (utilization >= 60 && utilization <= 80) return 100;
            if (utilization < 60) return utilization * 1.67; // Scale up to 100
            return Math.Max(0, 100 - (utilization - 80) * 5); // Penalize over-utilization
        }

        private List<string> GenerateOptimizationSuggestions(Dictionary<string, double> componentEfficiency)
        {
            var suggestions = new List<string>();

            foreach (var (component, efficiency) in componentEfficiency)
            {
                if (efficiency < 60)
                {
                    suggestions.Add($"Consider reducing {component} allocation as it's underutilized");
                }
                else if (efficiency < 40)
                {
                    suggestions.Add($"Critical: {component} is severely underutilized. Immediate resource reallocation recommended");
                }
                else if (efficiency > 90)
                {
                    suggestions.Add($"Warning: {component} is approaching maximum capacity. Consider scaling up");
                }
            }

            return suggestions;
        }
    }
}