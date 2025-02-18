using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public class HealthMonitor : IHealthMonitor
{
    private readonly ILogger<HealthMonitor> _logger;
    private readonly IClusterCoordinator _clusterCoordinator;
    private readonly ConcurrentDictionary<string, NodeHealth> _nodeHealth;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _unhealthyThreshold = TimeSpan.FromMinutes(2);
    private DateTime _lastClusterCheck = DateTime.MinValue;

    public HealthMonitor(
        ILogger<HealthMonitor> logger,
        IClusterCoordinator clusterCoordinator)
    {
        _logger = logger;
        _clusterCoordinator = clusterCoordinator;
        _nodeHealth = new ConcurrentDictionary<string, NodeHealth>();
    }

    public async Task<bool> IsNodeHealthy(string nodeId)
    {
        if (_nodeHealth.TryGetValue(nodeId, out var health))
        {
            return health.IsHealthy && 
                   DateTime.UtcNow - health.LastChecked < _healthCheckInterval;
        }

        // Perform on-demand health check
        var newHealth = await CheckNodeHealth(nodeId);
        _nodeHealth[nodeId] = newHealth;
        return newHealth.IsHealthy;
    }

    public async Task PerformClusterHealthCheck()
    {
        try
        {
            _lastClusterCheck = DateTime.UtcNow;
            var nodes = await _clusterCoordinator.GetActiveNodes();

            foreach (var node in nodes)
            {
                var health = await CheckNodeHealth(node.NodeId);
                _nodeHealth[node.NodeId] = health;

                if (!health.IsHealthy)
                {
                    await RaiseHealthAlert(new HealthAlert
                    {
                        NodeId = node.NodeId,
                        AlertType = "NodeUnhealthy",
                        Message = $"Node {node.NodeId} is unhealthy: {string.Join(", ", health.Issues)}",
                        Severity = AlertSeverity.Warning,
                        Context = new Dictionary<string, string>
                        {
                            ["CPU"] = $"{health.ResourceMetrics.CpuUsagePercent}%",
                            ["Memory"] = $"{health.ResourceMetrics.MemoryUsageMB}MB",
                            ["Uptime"] = $"{health.UptimeHours:F1}h"
                        }
                    });
                }
            }

            // Remove health records for nodes no longer in cluster
            var inactiveNodes = _nodeHealth.Keys.Except(nodes.Select(n => n.NodeId)).ToList();
            foreach (var nodeId in inactiveNodes)
            {
                _nodeHealth.TryRemove(nodeId, out _);
            }

            await DetectClusterIssues(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform cluster health check");
            throw;
        }
    }

    public async Task<List<NodeHealth>> GetClusterHealth()
    {
        // Refresh health check if needed
        if (DateTime.UtcNow - _lastClusterCheck > _healthCheckInterval)
        {
            await PerformClusterHealthCheck();
        }

        return _nodeHealth.Values.ToList();
    }

    public async Task RaiseHealthAlert(HealthAlert alert)
    {
        try
        {
            _logger.LogWarning("Health alert raised: {AlertType} for node {NodeId} - {Message}",
                alert.AlertType, alert.NodeId, alert.Message);

            // Implement alert distribution/notification
            await DistributeHealthAlert(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise health alert");
        }
    }

    private async Task<NodeHealth> CheckNodeHealth(string nodeId)
    {
        try
        {
            var metrics = await GetNodeMetrics(nodeId);
            var issues = new List<string>();

            if (metrics.CpuUsagePercent > 80)
            {
                issues.Add($"High CPU usage: {metrics.CpuUsagePercent}%");
            }

            if (metrics.MemoryUsageMB > metrics.TotalMemoryMB * 0.9)
            {
                issues.Add($"High memory usage: {metrics.MemoryUsageMB}MB/{metrics.TotalMemoryMB}MB");
            }

            if (metrics.DiskIoMBPS > 100)
            {
                issues.Add($"High disk I/O: {metrics.DiskIoMBPS}MB/s");
            }

            return new NodeHealth
            {
                NodeId = nodeId,
                IsHealthy = issues.Count == 0,
                ResourceMetrics = metrics,
                Issues = issues,
                UptimeHours = GetNodeUptime(nodeId),
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check health for node {NodeId}", nodeId);
            return new NodeHealth
            {
                NodeId = nodeId,
                IsHealthy = false,
                Issues = new List<string> { $"Health check failed: {ex.Message}" },
                LastChecked = DateTime.UtcNow
            };
        }
    }

    private async Task DetectClusterIssues(IEnumerable<NodeInfo> nodes)
    {
        var healthyNodes = _nodeHealth.Values.Count(h => h.IsHealthy);
        var totalNodes = nodes.Count();

        if (healthyNodes < totalNodes * 0.5) // Less than 50% healthy
        {
            await RaiseHealthAlert(new HealthAlert
            {
                NodeId = "Cluster",
                AlertType = "ClusterDegraded",
                Message = $"Cluster health degraded: Only {healthyNodes}/{totalNodes} nodes healthy",
                Severity = AlertSeverity.Critical
            });
        }

        // Check for resource imbalances
        var avgCpu = _nodeHealth.Values.Average(h => h.ResourceMetrics?.CpuUsagePercent ?? 0);
        var avgMemory = _nodeHealth.Values.Average(h => h.ResourceMetrics?.MemoryUsageMB ?? 0);

        foreach (var health in _nodeHealth.Values)
        {
            if (health.ResourceMetrics != null)
            {
                if (health.ResourceMetrics.CpuUsagePercent > avgCpu * 1.5)
                {
                    await RaiseHealthAlert(new HealthAlert
                    {
                        NodeId = health.NodeId,
                        AlertType = "ResourceImbalance",
                        Message = $"Node CPU usage significantly above cluster average",
                        Severity = AlertSeverity.Warning
                    });
                }

                if (health.ResourceMetrics.MemoryUsageMB > avgMemory * 1.5)
                {
                    await RaiseHealthAlert(new HealthAlert
                    {
                        NodeId = health.NodeId,
                        AlertType = "ResourceImbalance",
                        Message = $"Node memory usage significantly above cluster average",
                        Severity = AlertSeverity.Warning
                    });
                }
            }
        }
    }

    private async Task<ResourceMetrics> GetNodeMetrics(string nodeId)
    {
        // Implementation would make actual network call to node
        throw new NotImplementedException();
    }

    private double GetNodeUptime(string nodeId)
    {
        // Implementation would get actual node uptime
        return 0;
    }

    private async Task DistributeHealthAlert(HealthAlert alert)
    {
        // Implementation would distribute alert to monitoring systems
        throw new NotImplementedException();
    }
}