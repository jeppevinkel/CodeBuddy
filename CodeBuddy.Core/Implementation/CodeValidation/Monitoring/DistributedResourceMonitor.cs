using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class DistributedResourceMonitor : IDisposable
    {
        private readonly ValidationResilienceConfig _config;
        private readonly MetricsAggregator _metricsAggregator;
        private readonly ResourceAlertManager _alertManager;
        private readonly ConcurrentDictionary<string, NodeHealth> _nodeHealthRegistry;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _syncLock = new object();
        private bool _disposed;

        public DistributedResourceMonitor(
            ValidationResilienceConfig config,
            MetricsAggregator metricsAggregator,
            ResourceAlertManager alertManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _metricsAggregator = metricsAggregator ?? throw new ArgumentNullException(nameof(metricsAggregator));
            _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
            _nodeHealthRegistry = new ConcurrentDictionary<string, NodeHealth>();
            _cancellationTokenSource = new CancellationTokenSource();

            if (_config.EnableDistributedMonitoring)
            {
                InitializeDistributedMonitoring();
            }
        }

        public async Task RegisterNodeAsync(string nodeId, NodeCapabilities capabilities)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentNullException(nameof(nodeId));

            var nodeHealth = new NodeHealth
            {
                NodeId = nodeId,
                Capabilities = capabilities,
                LastHeartbeat = DateTime.UtcNow,
                Status = NodeStatus.Healthy
            };

            _nodeHealthRegistry.AddOrUpdate(nodeId, nodeHealth, (_, existing) =>
            {
                existing.LastHeartbeat = DateTime.UtcNow;
                return existing;
            });

            await PublishNodeMetricsAsync(nodeId);
        }

        public async Task UpdateNodeMetricsAsync(string nodeId, ResourceMetrics metrics)
        {
            if (!_nodeHealthRegistry.TryGetValue(nodeId, out var nodeHealth))
                throw new InvalidOperationException($"Node {nodeId} is not registered.");

            nodeHealth.CurrentMetrics = metrics;
            nodeHealth.LastHeartbeat = DateTime.UtcNow;

            await CheckResourceThresholdsAsync(nodeId, metrics);
            await _metricsAggregator.PublishMetricsAsync(nodeId, metrics);
        }

        public async Task<ClusterHealth> GetClusterHealthAsync()
        {
            var healthyNodes = 0;
            var totalNodes = _nodeHealthRegistry.Count;
            var aggregatedMetrics = new ResourceMetrics();

            foreach (var node in _nodeHealthRegistry.Values)
            {
                if (node.Status == NodeStatus.Healthy)
                {
                    healthyNodes++;
                    if (node.CurrentMetrics != null)
                    {
                        aggregatedMetrics.CpuUsage += node.CurrentMetrics.CpuUsage;
                        aggregatedMetrics.MemoryUsage += node.CurrentMetrics.MemoryUsage;
                        aggregatedMetrics.DiskIoUsage += node.CurrentMetrics.DiskIoUsage;
                    }
                }
            }

            return new ClusterHealth
            {
                HealthyNodeCount = healthyNodes,
                TotalNodeCount = totalNodes,
                AggregatedMetrics = new ResourceMetrics
                {
                    CpuUsage = totalNodes > 0 ? aggregatedMetrics.CpuUsage / totalNodes : 0,
                    MemoryUsage = totalNodes > 0 ? aggregatedMetrics.MemoryUsage / totalNodes : 0,
                    DiskIoUsage = totalNodes > 0 ? aggregatedMetrics.DiskIoUsage / totalNodes : 0
                },
                Status = healthyNodes >= _config.MinHealthyNodes ? ClusterStatus.Healthy : ClusterStatus.Degraded
            };
        }

        public async Task<bool> RequestWorkloadDistributionAsync(string nodeId, WorkloadRequest request)
        {
            var clusterHealth = await GetClusterHealthAsync();
            if (clusterHealth.Status != ClusterStatus.Healthy)
                return false;

            var targetNode = await SelectOptimalNodeAsync(request);
            if (targetNode == null)
                return false;

            return await DistributeWorkloadAsync(targetNode, request);
        }

        private async Task<string> SelectOptimalNodeAsync(WorkloadRequest request)
        {
            switch (_config.LoadBalancingStrategy)
            {
                case LoadBalancingStrategy.ResourceAware:
                    return await SelectResourceAwareNodeAsync(request);
                case LoadBalancingStrategy.LeastConnections:
                    return SelectLeastConnectionsNode();
                case LoadBalancingStrategy.Predictive:
                    return await SelectPredictiveNodeAsync(request);
                default:
                    return SelectRoundRobinNode();
            }
        }

        private async Task<string> SelectResourceAwareNodeAsync(WorkloadRequest request)
        {
            string selectedNode = null;
            double lowestResourceUsage = double.MaxValue;

            foreach (var node in _nodeHealthRegistry.Values)
            {
                if (node.Status != NodeStatus.Healthy || node.CurrentMetrics == null)
                    continue;

                var resourceScore = CalculateResourceScore(node.CurrentMetrics);
                if (resourceScore < lowestResourceUsage)
                {
                    lowestResourceUsage = resourceScore;
                    selectedNode = node.NodeId;
                }
            }

            return selectedNode;
        }

        private double CalculateResourceScore(ResourceMetrics metrics)
        {
            return (metrics.CpuUsage * 0.4) + (metrics.MemoryUsage * 0.4) + (metrics.DiskIoUsage * 0.2);
        }

        private async Task CheckResourceThresholdsAsync(string nodeId, ResourceMetrics metrics)
        {
            if (metrics.CpuUsage > _config.ClusterWideCpuThreshold ||
                metrics.MemoryUsage > _config.ClusterWideMemoryThreshold)
            {
                await _alertManager.RaiseResourceAlertAsync(new ResourceAlert
                {
                    NodeId = nodeId,
                    AlertType = AlertType.ResourceThresholdExceeded,
                    Message = $"Node {nodeId} exceeded cluster-wide resource thresholds",
                    Metrics = metrics,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private void InitializeDistributedMonitoring()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await PerformHealthChecksAsync();
                    await Task.Delay(_config.NodeHealthCheckInterval, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task PerformHealthChecksAsync()
        {
            var nodesToRemove = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var node in _nodeHealthRegistry)
            {
                var timeSinceLastHeartbeat = now - node.Value.LastHeartbeat;
                if (timeSinceLastHeartbeat > _config.NodeHealthCheckInterval * _config.NodeFailureThreshold)
                {
                    nodesToRemove.Add(node.Key);
                    await HandleNodeFailureAsync(node.Key);
                }
            }

            foreach (var nodeId in nodesToRemove)
            {
                _nodeHealthRegistry.TryRemove(nodeId, out _);
            }
        }

        private async Task HandleNodeFailureAsync(string nodeId)
        {
            await _alertManager.RaiseResourceAlertAsync(new ResourceAlert
            {
                NodeId = nodeId,
                AlertType = AlertType.NodeFailure,
                Message = $"Node {nodeId} has failed health checks",
                Timestamp = DateTime.UtcNow
            });

            if (_config.EnableAutomaticFailover)
            {
                await InitiateFailoverAsync(nodeId);
            }
        }

        private async Task InitiateFailoverAsync(string nodeId)
        {
            // Implement failover logic here
            await Task.CompletedTask;
        }

        private async Task PublishNodeMetricsAsync(string nodeId)
        {
            if (_nodeHealthRegistry.TryGetValue(nodeId, out var nodeHealth))
            {
                await _metricsAggregator.PublishMetricsAsync(nodeId, nodeHealth.CurrentMetrics ?? new ResourceMetrics());
            }
        }

        private string SelectRoundRobinNode()
        {
            var healthyNodes = _nodeHealthRegistry.Values
                .Where(n => n.Status == NodeStatus.Healthy)
                .Select(n => n.NodeId)
                .ToList();

            if (!healthyNodes.Any())
                return null;

            return healthyNodes[Interlocked.Increment(ref _roundRobinCounter) % healthyNodes.Count];
        }

        private string SelectLeastConnectionsNode()
        {
            return _nodeHealthRegistry.Values
                .Where(n => n.Status == NodeStatus.Healthy)
                .OrderBy(n => n.CurrentConnections)
                .FirstOrDefault()?.NodeId;
        }

        private async Task<string> SelectPredictiveNodeAsync(WorkloadRequest request)
        {
            // Implement predictive node selection based on historical performance
            return await Task.FromResult(SelectRoundRobinNode());
        }

        private async Task<bool> DistributeWorkloadAsync(string targetNodeId, WorkloadRequest request)
        {
            if (_nodeHealthRegistry.TryGetValue(targetNodeId, out var nodeHealth))
            {
                Interlocked.Increment(ref nodeHealth.CurrentConnections);
                // Implement workload distribution logic here
                return true;
            }
            return false;
        }

        private int _roundRobinCounter = -1;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }
    }

    public class NodeHealth
    {
        public string NodeId { get; set; }
        public NodeCapabilities Capabilities { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public NodeStatus Status { get; set; }
        public ResourceMetrics CurrentMetrics { get; set; }
        public int CurrentConnections;
    }

    public class NodeCapabilities
    {
        public int MaxConcurrentValidations { get; set; }
        public double AvailableCpuCores { get; set; }
        public double TotalMemoryMB { get; set; }
        public List<string> SupportedValidators { get; set; }
    }

    public class WorkloadRequest
    {
        public string RequestId { get; set; }
        public double EstimatedCpuUsage { get; set; }
        public double EstimatedMemoryUsage { get; set; }
        public int EstimatedDuration { get; set; }
        public ValidationPriority Priority { get; set; }
    }

    public class ClusterHealth
    {
        public int HealthyNodeCount { get; set; }
        public int TotalNodeCount { get; set; }
        public ResourceMetrics AggregatedMetrics { get; set; }
        public ClusterStatus Status { get; set; }
    }

    public enum NodeStatus
    {
        Healthy,
        Degraded,
        Failed
    }

    public enum ClusterStatus
    {
        Healthy,
        Degraded,
        Critical
    }

    public enum ValidationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}