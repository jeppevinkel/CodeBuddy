using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsAggregator
    {
        void RecordResourceUtilization(ResourceMetrics metrics);
        void RecordMiddlewareExecution(string middlewareName, bool success, TimeSpan duration);
        void RecordRetryAttempt(string middlewareName);
        void RecordCircuitBreakerStatus(string middlewareName, bool isOpen);
        RealtimeMetrics GetCurrentMetrics();
        Task<double> GetAverageResponseTimeAsync(TimeRange timeRange);
        Task<double> GetThroughputAsync(TimeRange timeRange);
        Task<double> GetErrorRateAsync(TimeRange timeRange);
        Task<int> GetConcurrentOperationsAsync();
        Task<double> GetResourceEfficiencyAsync(TimeRange timeRange);
    }

    public class MetricsAggregator : IMetricsAggregator
    {
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly ResourceMetricsBuffer _metricsBuffer;

        public MetricsAggregator(IResourceAnalytics resourceAnalytics)
        {
            _resourceAnalytics = resourceAnalytics;
            _metricsBuffer = new ResourceMetricsBuffer();
        }

        public void RecordResourceUtilization(ResourceMetrics metrics)
        {
            _metricsBuffer.AddMetrics(metrics);
        }

        public void RecordMiddlewareExecution(string middlewareName, bool success, TimeSpan duration)
        {
            _metricsBuffer.AddMiddlewareExecution(middlewareName, success, duration);
        }

        public void RecordRetryAttempt(string middlewareName)
        {
            _metricsBuffer.AddRetryAttempt(middlewareName);
        }

        public void RecordCircuitBreakerStatus(string middlewareName, bool isOpen)
        {
            _metricsBuffer.UpdateCircuitBreakerStatus(middlewareName, isOpen);
        }

        public RealtimeMetrics GetCurrentMetrics()
        {
            var metrics = _metricsBuffer.GetCurrentMetrics();
            return new RealtimeMetrics
            {
                CpuUsagePercent = metrics.CpuUsagePercent,
                PeakCpuUsage = _metricsBuffer.GetPeakCpuUsage(),
                AverageCpuUsage = _metricsBuffer.GetAverageCpuUsage(),
                CpuUsageTrend = _metricsBuffer.GetCpuUsageTrend(),

                MemoryUsagePercent = metrics.MemoryUsageMB,
                PeakMemoryUsage = _metricsBuffer.GetPeakMemoryUsage(),
                AverageMemoryUsage = _metricsBuffer.GetAverageMemoryUsage(),
                MemoryUsageTrend = _metricsBuffer.GetMemoryUsageTrend(),

                DiskUtilizationPercent = metrics.DiskIoMBPS,
                PeakDiskUtilization = _metricsBuffer.GetPeakDiskUtilization(),
                AverageDiskUtilization = _metricsBuffer.GetAverageDiskUtilization(),
                DiskUtilizationTrend = _metricsBuffer.GetDiskUtilizationTrend(),

                NetworkUtilizationPercent = metrics.NetworkBandwidthUsage,
                PeakNetworkUtilization = _metricsBuffer.GetPeakNetworkUtilization(),
                AverageNetworkUtilization = _metricsBuffer.GetAverageNetworkUtilization(),
                NetworkUtilizationTrend = _metricsBuffer.GetNetworkUtilizationTrend()
            };
        }

        public async Task<double> GetAverageResponseTimeAsync(TimeRange timeRange)
        {
            return await _resourceAnalytics.GetAverageResponseTimeAsync(timeRange.Start, timeRange.End);
        }

        public async Task<double> GetThroughputAsync(TimeRange timeRange)
        {
            return await _resourceAnalytics.GetThroughputAsync(timeRange.Start, timeRange.End);
        }

        public async Task<double> GetErrorRateAsync(TimeRange timeRange)
        {
            return await _resourceAnalytics.GetErrorRateAsync(timeRange.Start, timeRange.End);
        }

        public async Task<int> GetConcurrentOperationsAsync()
        {
            return _metricsBuffer.GetConcurrentOperations();
        }

        public async Task<double> GetResourceEfficiencyAsync(TimeRange timeRange)
        {
            return await _resourceAnalytics.GetResourceEfficiencyAsync(timeRange.Start, timeRange.End);
        }
    }
}