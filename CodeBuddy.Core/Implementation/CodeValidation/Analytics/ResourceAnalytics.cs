using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public interface IResourceAnalytics
    {
        Task StoreResourceUsageDataAsync(ResourceUsageData data);
        Task StoreResponseTimeDataAsync(ResponseTimeData data);
        Task<List<ResourceBottleneck>> IdentifyBottlenecksAsync();
    }

    public class ResourceAnalytics : IResourceAnalytics
    {
        private readonly TimeSeriesStorage _timeSeriesStorage;

        public ResourceAnalytics(TimeSeriesStorage timeSeriesStorage)
        {
            _timeSeriesStorage = timeSeriesStorage;
        }

        public async Task StoreResourceUsageDataAsync(ResourceUsageData data)
        {
            await _timeSeriesStorage.StoreTimeSeriesDataAsync("resource_usage", new Dictionary<string, double>
            {
                ["cpu_usage"] = data.CpuUsagePercentage,
                ["memory_usage"] = data.MemoryUsageMB,
                ["disk_io"] = data.DiskIOBytesPerSecond
            }, data.Timestamp);
        }

        public async Task StoreResponseTimeDataAsync(ResponseTimeData data)
        {
            await _timeSeriesStorage.StoreTimeSeriesDataAsync("response_time", new Dictionary<string, double>
            {
                ["avg_response_time"] = data.AverageResponseTime,
                ["p95_response_time"] = data.P95ResponseTime,
                ["p99_response_time"] = data.P99ResponseTime,
                ["slow_request_percentage"] = data.SlowRequestPercentage,
                ["total_requests"] = data.TotalRequests,
                ["slow_requests"] = data.SlowRequests
            }, data.Timestamp);
        }

        public async Task<List<ResourceBottleneck>> IdentifyBottlenecksAsync()
        {
            var bottlenecks = new List<ResourceBottleneck>();
            var resourceData = await _timeSeriesStorage.GetTimeSeriesDataAsync("resource_usage", TimeSpan.FromHours(1));
            var responseTimeData = await _timeSeriesStorage.GetTimeSeriesDataAsync("response_time", TimeSpan.FromHours(1));

            // Analyze CPU usage trends
            if (resourceData.TryGetValue("cpu_usage", out var cpuTrend) && 
                CalculateTrend(cpuTrend) > 0.1)
            {
                bottlenecks.Add(new ResourceBottleneck
                {
                    ResourceType = ResourceMetricType.CPU,
                    Impact = "Increasing CPU usage trend detected",
                    RecommendedAction = "Consider scaling out or optimizing CPU-intensive operations"
                });
            }

            // Analyze memory usage trends
            if (resourceData.TryGetValue("memory_usage", out var memoryTrend) && 
                CalculateTrend(memoryTrend) > 0.1)
            {
                bottlenecks.Add(new ResourceBottleneck
                {
                    ResourceType = ResourceMetricType.Memory,
                    Impact = "Increasing memory usage trend detected",
                    RecommendedAction = "Check for memory leaks or consider increasing memory allocation"
                });
            }

            // Analyze response time trends
            if (responseTimeData.TryGetValue("avg_response_time", out var responseTrend) && 
                CalculateTrend(responseTrend) > 0.1)
            {
                bottlenecks.Add(new ResourceBottleneck
                {
                    ResourceType = ResourceMetricType.ResponseTime,
                    Impact = "Degrading response time trend detected",
                    RecommendedAction = "Investigate performance bottlenecks and consider scaling resources"
                });
            }

            return bottlenecks;
        }

        private double CalculateTrend(List<TimeSeriesDataPoint> dataPoints)
        {
            if (dataPoints.Count < 2)
                return 0;

            // Simple linear regression
            var n = dataPoints.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumXX = 0.0;

            for (var i = 0; i < n; i++)
            {
                var x = i;
                var y = dataPoints[i].Value;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            return (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        }
    }
}