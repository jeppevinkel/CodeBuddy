using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IValidationPipelineDashboard
    {
        Task<DashboardData> GetDashboardDataAsync();
        Task UpdateMetricsAsync(ValidationPipelineMetrics metrics);
    }

    public class ValidationPipelineDashboard : IValidationPipelineDashboard
    {
        private readonly TimeSeriesStorage _timeSeriesStorage;
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly ConcurrentDictionary<string, DashboardData> _cachedDashboardData;
        private readonly TimeSpan _updateInterval;
        private readonly ResponseTimeChart _responseTimeChart;

        public ValidationPipelineDashboard(
            TimeSeriesStorage timeSeriesStorage,
            IResourceAnalytics resourceAnalytics)
        {
            _timeSeriesStorage = timeSeriesStorage;
            _resourceAnalytics = resourceAnalytics;
            _cachedDashboardData = new ConcurrentDictionary<string, DashboardData>();
            _updateInterval = TimeSpan.FromSeconds(1);
            _responseTimeChart = new ResponseTimeChart();
            StartPeriodicUpdate();
        }

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            if (_cachedDashboardData.TryGetValue("current", out var cachedData) &&
                DateTime.UtcNow - cachedData.LastUpdated < _updateInterval)
            {
                return cachedData;
            }

            var dashboardData = await BuildDashboardDataAsync();
            _cachedDashboardData.AddOrUpdate("current", dashboardData, (_, __) => dashboardData);
            return dashboardData;
        }

        public async Task UpdateMetricsAsync(ValidationPipelineMetrics metrics)
        {
            await _timeSeriesStorage.StoreTimeSeriesDataAsync("pipeline_metrics", new Dictionary<string, double>
            {
                ["total_validations"] = metrics.TotalValidations,
                ["successful_validations"] = metrics.SuccessfulValidations,
                ["failed_validations"] = metrics.FailedValidations,
                ["average_response_time"] = metrics.AverageResponseTime.TotalMilliseconds,
                ["p95_response_time"] = metrics.P95ResponseTime.TotalMilliseconds,
                ["p99_response_time"] = metrics.P99ResponseTime.TotalMilliseconds,
                ["slow_request_percentage"] = metrics.SlowRequestPercentage
            }, DateTime.UtcNow);

            _responseTimeChart.UpdateData(metrics);
        }

        private async Task<DashboardData> BuildDashboardDataAsync()
        {
            var metricsData = await _timeSeriesStorage.GetTimeSeriesDataAsync("pipeline_metrics", TimeSpan.FromHours(1));
            var resourceData = await _timeSeriesStorage.GetTimeSeriesDataAsync("resource_usage", TimeSpan.FromHours(1));
            var responseTimeData = await _timeSeriesStorage.GetTimeSeriesDataAsync("response_time", TimeSpan.FromHours(1));

            return new DashboardData
            {
                PipelineMetrics = BuildPipelineMetrics(metricsData),
                ResourceUtilization = BuildResourceUtilization(resourceData),
                ResponseTimeMetrics = BuildResponseTimeMetrics(responseTimeData),
                ResponseTimeChart = _responseTimeChart.GetChartData(),
                LastUpdated = DateTime.UtcNow
            };
        }

        private void StartPeriodicUpdate()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await GetDashboardDataAsync();
                    await Task.Delay(_updateInterval);
                }
            });
        }

        private ValidationPipelineMetrics BuildPipelineMetrics(Dictionary<string, List<TimeSeriesDataPoint>> data)
        {
            if (!data.Any())
                return new ValidationPipelineMetrics();

            var latestData = data.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderByDescending(p => p.Timestamp).First().Value);

            return new ValidationPipelineMetrics
            {
                TotalValidations = (int)latestData.GetValueOrDefault("total_validations"),
                SuccessfulValidations = (int)latestData.GetValueOrDefault("successful_validations"),
                FailedValidations = (int)latestData.GetValueOrDefault("failed_validations"),
                AverageResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("average_response_time")),
                P95ResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("p95_response_time")),
                P99ResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("p99_response_time")),
                SlowRequestPercentage = latestData.GetValueOrDefault("slow_request_percentage")
            };
        }

        private ResourceUtilization BuildResourceUtilization(Dictionary<string, List<TimeSeriesDataPoint>> data)
        {
            if (!data.Any())
                return new ResourceUtilization();

            var latestData = data.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderByDescending(p => p.Timestamp).First().Value);

            return new ResourceUtilization
            {
                CpuUsagePercent = latestData.GetValueOrDefault("cpu_usage"),
                MemoryUsageMB = latestData.GetValueOrDefault("memory_usage"),
                DiskIoMBPS = latestData.GetValueOrDefault("disk_io") / (1024 * 1024)
            };
        }

        private ResponseTimeMetrics BuildResponseTimeMetrics(Dictionary<string, List<TimeSeriesDataPoint>> data)
        {
            if (!data.Any())
                return new ResponseTimeMetrics();

            var latestData = data.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderByDescending(p => p.Timestamp).First().Value);

            return new ResponseTimeMetrics
            {
                AverageResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("avg_response_time")),
                P95ResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("p95_response_time")),
                P99ResponseTime = TimeSpan.FromMilliseconds(latestData.GetValueOrDefault("p99_response_time")),
                SlowRequestPercentage = latestData.GetValueOrDefault("slow_request_percentage"),
                TotalRequests = (int)latestData.GetValueOrDefault("total_requests"),
                SlowRequests = (int)latestData.GetValueOrDefault("slow_requests")
            };
        }
    }

    public class ResponseTimeChart
    {
        private readonly ConcurrentQueue<ResponseTimeDataPoint> _dataPoints;
        private readonly int _maxDataPoints = 100;

        public ResponseTimeChart()
        {
            _dataPoints = new ConcurrentQueue<ResponseTimeDataPoint>();
        }

        public void UpdateData(ValidationPipelineMetrics metrics)
        {
            _dataPoints.Enqueue(new ResponseTimeDataPoint
            {
                Timestamp = DateTime.UtcNow,
                AverageResponseTime = metrics.AverageResponseTime.TotalMilliseconds,
                P95ResponseTime = metrics.P95ResponseTime.TotalMilliseconds,
                P99ResponseTime = metrics.P99ResponseTime.TotalMilliseconds
            });

            while (_dataPoints.Count > _maxDataPoints)
            {
                _dataPoints.TryDequeue(out _);
            }
        }

        public ChartData GetChartData()
        {
            var points = _dataPoints.OrderBy(p => p.Timestamp).ToList();
            return new ChartData
            {
                Labels = points.Select(p => p.Timestamp.ToString("HH:mm:ss")).ToList(),
                Datasets = new[]
                {
                    new ChartDataset
                    {
                        Label = "Average Response Time",
                        Data = points.Select(p => p.AverageResponseTime).ToList()
                    },
                    new ChartDataset
                    {
                        Label = "P95 Response Time",
                        Data = points.Select(p => p.P95ResponseTime).ToList()
                    },
                    new ChartDataset
                    {
                        Label = "P99 Response Time",
                        Data = points.Select(p => p.P99ResponseTime).ToList()
                    }
                }
            };
        }
    }

    public class ResponseTimeDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double AverageResponseTime { get; set; }
        public double P95ResponseTime { get; set; }
        public double P99ResponseTime { get; set; }
    }

    public class ChartData
    {
        public List<string> Labels { get; set; }
        public ChartDataset[] Datasets { get; set; }
    }

    public class ChartDataset
    {
        public string Label { get; set; }
        public List<double> Data { get; set; }
    }

    public class DashboardData
    {
        public ValidationPipelineMetrics PipelineMetrics { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public ResponseTimeMetrics ResponseTimeMetrics { get; set; }
        public ChartData ResponseTimeChart { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}