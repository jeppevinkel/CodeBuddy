using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ClusterDashboard
    {
        private readonly DistributedResourceMonitor _resourceMonitor;
        private readonly MetricsAggregator _metricsAggregator;
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly ValidationResilienceConfig _config;

        public ClusterDashboard(
            DistributedResourceMonitor resourceMonitor,
            MetricsAggregator metricsAggregator,
            ResourceTrendAnalyzer trendAnalyzer,
            ValidationResilienceConfig config)
        {
            _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
            _metricsAggregator = metricsAggregator ?? throw new ArgumentNullException(nameof(metricsAggregator));
            _trendAnalyzer = trendAnalyzer ?? throw new ArgumentNullException(nameof(trendAnalyzer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<ClusterDashboardData> GetDashboardDataAsync()
        {
            var clusterHealth = await _resourceMonitor.GetClusterHealthAsync();
            var clusterMetrics = await _metricsAggregator.GetClusterMetricsAsync();
            var trends = await _trendAnalyzer.AnalyzeClusterTrendsAsync();

            return new ClusterDashboardData
            {
                ClusterHealth = clusterHealth,
                ClusterMetrics = clusterMetrics,
                ResourceTrends = trends,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<NodeDashboardData> GetNodeDashboardDataAsync(string nodeId)
        {
            var nodeMetrics = await _metricsAggregator.GetNodeMetricsAsync();
            if (!nodeMetrics.TryGetValue(nodeId, out var metrics))
            {
                throw new KeyNotFoundException($"Node {nodeId} not found");
            }

            var trends = await _trendAnalyzer.AnalyzeNodeTrendsAsync(nodeId);

            return new NodeDashboardData
            {
                NodeId = nodeId,
                CurrentMetrics = metrics,
                ResourceTrends = trends,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<List<ResourceAlert>> GetActiveAlertsAsync()
        {
            // Get all active alerts across the cluster
            return await Task.FromResult(new List<ResourceAlert>());
        }

        public async Task<ClusterCapacityReport> GetCapacityReportAsync()
        {
            var clusterMetrics = await _metricsAggregator.GetClusterMetricsAsync();
            var trends = await _trendAnalyzer.AnalyzeClusterTrendsAsync();

            return new ClusterCapacityReport
            {
                CurrentCapacity = CalculateCurrentCapacity(clusterMetrics),
                ProjectedCapacity = CalculateProjectedCapacity(trends),
                RecommendedActions = GenerateCapacityRecommendations(clusterMetrics, trends),
                Timestamp = DateTime.UtcNow
            };
        }

        private ClusterCapacity CalculateCurrentCapacity(ClusterMetrics metrics)
        {
            return new ClusterCapacity
            {
                TotalCpuCapacity = metrics.ActiveNodes * 100, // Assuming 100% per node
                UsedCpuCapacity = metrics.AggregatedMetrics.CpuUsagePercent * metrics.ActiveNodes,
                TotalMemoryCapacity = metrics.ActiveNodes * _config.MaxMemoryThresholdMB,
                UsedMemoryCapacity = metrics.AggregatedMetrics.MemoryUsageMB * metrics.ActiveNodes,
                AvailableNodes = metrics.ActiveNodes,
                MaxNodes = _config.MaxNodesInCluster
            };
        }

        private ProjectedCapacity CalculateProjectedCapacity(ResourceTrends trends)
        {
            return new ProjectedCapacity
            {
                ProjectedCpuUsage = trends.CpuTrend.ProjectedValue,
                ProjectedMemoryUsage = trends.MemoryTrend.ProjectedValue,
                TimeToCapacityLimit = trends.TimeToResourceExhaustion,
                ConfidenceLevel = trends.TrendConfidence
            };
        }

        private List<CapacityRecommendation> GenerateCapacityRecommendations(
            ClusterMetrics metrics, ResourceTrends trends)
        {
            var recommendations = new List<CapacityRecommendation>();

            // Add node recommendation
            if (ShouldAddNode(metrics, trends))
            {
                recommendations.Add(new CapacityRecommendation
                {
                    Action = RecommendedAction.AddNode,
                    Priority = RecommendationPriority.High,
                    Reason = "Projected resource usage exceeds capacity threshold"
                });
            }

            // Optimize resource distribution
            if (HasResourceImbalance(metrics))
            {
                recommendations.Add(new CapacityRecommendation
                {
                    Action = RecommendedAction.RebalanceResources,
                    Priority = RecommendationPriority.Medium,
                    Reason = "Resource utilization is imbalanced across nodes"
                });
            }

            return recommendations;
        }

        private bool ShouldAddNode(ClusterMetrics metrics, ResourceTrends trends)
        {
            return metrics.AggregatedMetrics.CpuUsagePercent > _config.ClusterWideCpuThreshold ||
                   metrics.AggregatedMetrics.MemoryUsageMB > _config.ClusterWideMemoryThreshold ||
                   trends.TimeToResourceExhaustion < TimeSpan.FromHours(1);
        }

        private bool HasResourceImbalance(ClusterMetrics metrics)
        {
            if (metrics.NodeMetrics.Count <= 1)
                return false;

            var cpuUsages = metrics.NodeMetrics.Values.Select(m => m.CpuUsagePercent);
            var memoryUsages = metrics.NodeMetrics.Values.Select(m => m.MemoryUsageMB);

            return CalculateStandardDeviation(cpuUsages) > 20 || // 20% variation threshold
                   CalculateStandardDeviation(memoryUsages) > _config.MaxMemoryThresholdMB * 0.2;
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (!list.Any()) return 0;

            var avg = list.Average();
            var sum = list.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / list.Count);
        }
    }

    public class ClusterDashboardData
    {
        public ClusterHealth ClusterHealth { get; set; }
        public ClusterMetrics ClusterMetrics { get; set; }
        public ResourceTrends ResourceTrends { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NodeDashboardData
    {
        public string NodeId { get; set; }
        public ResourceMetrics CurrentMetrics { get; set; }
        public ResourceTrends ResourceTrends { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ClusterCapacityReport
    {
        public ClusterCapacity CurrentCapacity { get; set; }
        public ProjectedCapacity ProjectedCapacity { get; set; }
        public List<CapacityRecommendation> RecommendedActions { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ClusterCapacity
    {
        public double TotalCpuCapacity { get; set; }
        public double UsedCpuCapacity { get; set; }
        public double TotalMemoryCapacity { get; set; }
        public double UsedMemoryCapacity { get; set; }
        public int AvailableNodes { get; set; }
        public int MaxNodes { get; set; }
    }

    public class ProjectedCapacity
    {
        public double ProjectedCpuUsage { get; set; }
        public double ProjectedMemoryUsage { get; set; }
        public TimeSpan TimeToCapacityLimit { get; set; }
        public double ConfidenceLevel { get; set; }
    }

    public class CapacityRecommendation
    {
        public RecommendedAction Action { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Reason { get; set; }
    }

    public enum RecommendedAction
    {
        AddNode,
        RemoveNode,
        RebalanceResources,
        OptimizeResourceUsage,
        UpgradeHardware
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}