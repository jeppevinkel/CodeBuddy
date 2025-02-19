using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.AspNetCore.SignalR;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace CodeBuddy.Core.Implementation.CodeValidation.Analytics
{
    public class ResourceAnalyticsDashboard
    {
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly ResourceMonitoringDashboard _resourceMonitor;
        private readonly IHubContext<ResourceMetricsHub> _hubContext;
        private readonly ILogger<ResourceAnalyticsDashboard> _logger;
        private readonly Timer _updateTimer;
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);

        public ResourceAnalyticsDashboard(
            IResourceAnalytics resourceAnalytics,
            IMetricsAggregator metricsAggregator,
            ResourceMonitoringDashboard resourceMonitor,
            IHubContext<ResourceMetricsHub> hubContext,
            ILogger<ResourceAnalyticsDashboard> logger)
        {
            _resourceAnalytics = resourceAnalytics;
            _metricsAggregator = metricsAggregator;
            _resourceMonitor = resourceMonitor;
            _hubContext = hubContext;
            _logger = logger;
            _updateTimer = new Timer(UpdateDashboard, null, _updateInterval, _updateInterval);
        }

        private async void UpdateDashboard(object state)
        {
            try
            {
                var dashboardData = await GetDashboardDataAsync();
                await _hubContext.Clients.All.SendAsync("UpdateDashboard", dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating resource analytics dashboard");
            }
        }

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var currentMetrics = await _resourceMonitor.GetCurrentMetricsAsync();
            var usageTrends = await _resourceAnalytics.AnalyzeUsageTrendsAsync();
            var bottlenecks = await _resourceAnalytics.IdentifyBottlenecksAsync();
            var recommendations = await _resourceAnalytics.GetOptimizationRecommendationsAsync();

            return new DashboardData
            {
                CurrentMetrics = currentMetrics,
                Trends = usageTrends,
                Bottlenecks = bottlenecks.ToList(),
                Recommendations = recommendations.ToList(),
                Alerts = _resourceMonitor.GetActiveAlerts().ToList(),
                PerformanceMetrics = await GetPerformanceMetricsAsync(),
                ResourceUtilization = await GetResourceUtilizationAsync()
            };
        }

        public async Task<IEnumerable<ResourceWidget>> GetCustomWidgetsAsync(string userId)
        {
            // Implement custom widget retrieval based on user preferences
            return new List<ResourceWidget>();
        }

        public async Task<byte[]> ExportReportAsync(TimeSpan period, string format)
        {
            var report = await _resourceAnalytics.GenerateReportAsync(period);
            return GenerateReport(report, format);
        }

        public async Task<Dictionary<string, AlertThreshold>> GetAlertThresholdsAsync()
        {
            return new Dictionary<string, AlertThreshold>
            {
                { "CPU", new AlertThreshold { Warning = 70, Critical = 90 } },
                { "Memory", new AlertThreshold { Warning = 80, Critical = 95 } },
                { "DiskIO", new AlertThreshold { Warning = 75, Critical = 90 } }
            };
        }

        public async Task UpdateAlertThresholdsAsync(Dictionary<string, AlertThreshold> thresholds)
        {
            // Implement threshold update logic
        }

        private async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
        {
            var timeSeriesData = await _metricsAggregator.GetTimeSeriesDataAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
            return new PerformanceMetrics
            {
                ValidatorPerformance = await GetValidatorPerformanceAsync(),
                CacheMetrics = await GetCacheMetricsAsync(),
                ResourceEfficiency = CalculateResourceEfficiency(timeSeriesData)
            };
        }

        private async Task<ResourceUtilization> GetResourceUtilizationAsync()
        {
            var currentData = await _metricsAggregator.GetCurrentMetricsAsync();
            return new ResourceUtilization
            {
                CpuUtilization = currentData.CpuUsagePercent,
                MemoryUtilization = (double)currentData.MemoryUsageBytes / Environment.SystemPageSize,
                DiskUtilization = currentData.DiskIORate,
                NetworkUtilization = currentData.NetworkUsage
            };
        }

        private async Task<Dictionary<string, double>> GetValidatorPerformanceAsync()
        {
            var metrics = await _metricsAggregator.GetCurrentMetricsAsync();
            return new Dictionary<string, double>
            {
                { "ThroughputPerSecond", metrics.ValidatorThroughput },
                { "AverageLatencyMs", metrics.ValidatorLatency },
                { "ErrorRate", metrics.ValidatorErrorRate },
                { "QueueLength", metrics.ValidatorQueueLength },
                { "SuccessRate", 100 - metrics.ValidatorErrorRate },
                { "ProcessingTime", metrics.ValidatorProcessingTime }
            };
        }

        private async Task<CacheMetrics> GetCacheMetricsAsync()
        {
            var currentMetrics = await _metricsAggregator.GetCurrentMetricsAsync();
            return new CacheMetrics
            {
                HitRate = currentMetrics.CacheHitRate,
                MissRate = currentMetrics.CacheMissRate,
                EvictionRate = currentMetrics.CacheEvictionRate,
                AverageAccessTime = currentMetrics.CacheAccessTime,
                TotalEntries = currentMetrics.CacheEntryCount,
                MemoryUsage = currentMetrics.CacheMemoryUsage,
                EfficiencyScore = CalculateCacheEfficiencyScore(currentMetrics)
            };
        }

        private ResourceEfficiency CalculateResourceEfficiency(IEnumerable<TimeSeriesDataPoint> data)
        {
            if (!data.Any())
                return new ResourceEfficiency();

            var latest = data.OrderByDescending(d => d.Timestamp).First();
            var componentEfficiency = new Dictionary<string, double>
            {
                { "CPU", CalculateComponentEfficiency(latest.CpuUsagePercent, 60, 80) },
                { "Memory", CalculateComponentEfficiency(latest.MemoryUsagePercent, 70, 85) },
                { "DiskIO", CalculateComponentEfficiency(latest.DiskIORate, 50, 75) },
                { "Network", CalculateComponentEfficiency(latest.NetworkUsage, 40, 70) }
            };

            return new ResourceEfficiency
            {
                ResourceUtilizationScore = componentEfficiency.Values.Average(),
                ResourceWasteScore = 100 - componentEfficiency.Values.Average(),
                ComponentEfficiency = componentEfficiency,
                OptimizationSuggestions = GenerateOptimizationSuggestions(componentEfficiency)
            };
        }

        private byte[] GenerateReport(ResourceUsageReport report, string format)
        {
            using var memoryStream = new MemoryStream();
            
            switch (format.ToLower())
            {
                case "pdf":
                    GeneratePdfReport(report, memoryStream);
                    break;
                case "excel":
                    GenerateExcelReport(report, memoryStream);
                    break;
                case "csv":
                    GenerateCsvReport(report, memoryStream);
                    break;
                case "json":
                    GenerateJsonReport(report, memoryStream);
                    break;
                default:
                    throw new ArgumentException($"Unsupported report format: {format}");
            }

            return memoryStream.ToArray();
        }

        private void GeneratePdfReport(ResourceUsageReport report, Stream outputStream)
        {
            // Implementation of PDF report generation
            using var document = new Document();
            var writer = PdfWriter.GetInstance(document, outputStream);
            document.Open();

            // Add title
            document.Add(new Paragraph($"Resource Usage Report - {report.StartTime:yyyy-MM-dd} to {report.EndTime:yyyy-MM-dd}"));

            // Add resource utilization section
            AddResourceUtilizationSection(document, report);

            // Add performance metrics section
            AddPerformanceMetricsSection(document, report);

            // Add trends and analysis section
            AddTrendsAndAnalysisSection(document, report);

            // Add recommendations section
            AddRecommendationsSection(document, report);

            document.Close();
        }

        private void GenerateExcelReport(ResourceUsageReport report, Stream outputStream)
        {
            using var workbook = new XLWorkbook();
            
            // Resource Utilization Sheet
            var utilizationSheet = workbook.Addworksheet("Resource Utilization");
            AddResourceUtilizationToExcel(utilizationSheet, report);

            // Performance Metrics Sheet
            var performanceSheet = workbook.Addworksheet("Performance Metrics");
            AddPerformanceMetricsToExcel(performanceSheet, report);

            // Trends Sheet
            var trendsSheet = workbook.Addworksheet("Trends & Analysis");
            AddTrendsToExcel(trendsSheet, report);

            // Recommendations Sheet
            var recommendationsSheet = workbook.Addworksheet("Recommendations");
            AddRecommendationsToExcel(recommendationsSheet, report);

            workbook.SaveAs(outputStream);
        }

        private void GenerateCsvReport(ResourceUsageReport report, Stream outputStream)
        {
            using var writer = new StreamWriter(outputStream);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write header
            csv.WriteHeader<ResourceMetricsRecord>();
            csv.NextRecord();

            // Write resource metrics
            foreach (var metric in report.TimeSeriesData)
            {
                csv.WriteRecord(new ResourceMetricsRecord
                {
                    Timestamp = metric.Timestamp,
                    CpuUsage = metric.Metrics["CpuUsage"],
                    MemoryUsage = metric.Metrics["MemoryUsage"],
                    DiskIORate = metric.Metrics["DiskIORate"],
                    NetworkUsage = metric.Metrics.GetValueOrDefault("NetworkUsage", 0)
                });
                csv.NextRecord();
            }
        }

        private void GenerateJsonReport(ResourceUsageReport report, Stream outputStream)
        {
            using var writer = new StreamWriter(outputStream);
            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            var json = JsonConvert.SerializeObject(report, jsonSettings);
            writer.Write(json);
        }

        private double CalculateComponentEfficiency(double utilization, double optimalMin, double optimalMax)
        {
            if (utilization >= optimalMin && utilization <= optimalMax) return 100;
            if (utilization < optimalMin) return (utilization / optimalMin) * 100;
            return Math.Max(0, 100 - ((utilization - optimalMax) / (100 - optimalMax)) * 100);
        }

        private double CalculateCacheEfficiencyScore(ResourceMetrics metrics)
        {
            double hitRateWeight = 0.4;
            double accessTimeWeight = 0.3;
            double memoryUsageWeight = 0.3;

            double hitRateScore = metrics.CacheHitRate;
            double accessTimeScore = 100 - Math.Min(100, (metrics.CacheAccessTime / 10) * 100); // Normalize to 0-100
            double memoryUsageScore = 100 - metrics.CacheMemoryUsage;

            return (hitRateScore * hitRateWeight) +
                   (accessTimeScore * accessTimeWeight) +
                   (memoryUsageScore * memoryUsageWeight);
        }
    }

    public class ResourceMetricsHub : Hub
    {
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly IMetricsAggregator _metricsAggregator;
        private readonly ILogger<ResourceMetricsHub> _logger;

        public ResourceMetricsHub(
            IResourceAnalytics resourceAnalytics,
            IMetricsAggregator metricsAggregator,
            ILogger<ResourceMetricsHub> logger)
        {
            _resourceAnalytics = resourceAnalytics;
            _metricsAggregator = metricsAggregator;
            _logger = logger;
        }

        public async Task SubscribeToMetrics(string[] metricTypes)
        {
            try
            {
                foreach (var metricType in metricTypes)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, metricType);
                    _logger.LogInformation($"Client {Context.ConnectionId} subscribed to {metricType}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error subscribing client {Context.ConnectionId} to metrics");
                throw;
            }
        }

        public async Task UnsubscribeFromMetrics(string[] metricTypes)
        {
            try
            {
                foreach (var metricType in metricTypes)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, metricType);
                    _logger.LogInformation($"Client {Context.ConnectionId} unsubscribed from {metricType}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unsubscribing client {Context.ConnectionId} from metrics");
                throw;
            }
        }

        public async Task RequestHistoricalData(DateTime startTime, DateTime endTime, string[] metricTypes)
        {
            try
            {
                var historicalData = await _metricsAggregator.GetTimeSeriesDataAsync(startTime, endTime);
                var filteredData = FilterMetricsByType(historicalData, metricTypes);
                await Clients.Caller.SendAsync("HistoricalDataResponse", filteredData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical data");
                await Clients.Caller.SendAsync("Error", "Failed to retrieve historical data");
                throw;
            }
        }

        public async Task RequestResourceAnalytics(TimeSpan period)
        {
            try
            {
                var analytics = await _resourceAnalytics.GenerateReportAsync(period);
                await Clients.Caller.SendAsync("ResourceAnalyticsResponse", analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving resource analytics");
                await Clients.Caller.SendAsync("Error", "Failed to retrieve resource analytics");
                throw;
            }
        }

        public async Task RequestOptimizationRecommendations()
        {
            try
            {
                var recommendations = await _resourceAnalytics.GetOptimizationRecommendationsAsync();
                await Clients.Caller.SendAsync("OptimizationRecommendationsResponse", recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization recommendations");
                await Clients.Caller.SendAsync("Error", "Failed to retrieve optimization recommendations");
                throw;
            }
        }

        private IEnumerable<TimeSeriesDataPoint> FilterMetricsByType(
            IEnumerable<TimeSeriesDataPoint> data,
            string[] metricTypes)
        {
            return data.Select(point => new TimeSeriesDataPoint
            {
                Timestamp = point.Timestamp,
                Metrics = point.Metrics
                    .Where(m => metricTypes.Contains(m.Key))
                    .ToDictionary(m => m.Key, m => m.Value),
                Tags = point.Tags
            });
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            if (exception != null)
            {
                _logger.LogError(exception, $"Client {Context.ConnectionId} disconnected with error");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class ResourceMetricsRecord
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskIORate { get; set; }
        public double NetworkUsage { get; set; }
    }
}