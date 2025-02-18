using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public class LoadBalancer : ILoadBalancer
{
    private readonly ILogger<LoadBalancer> _logger;
    private readonly IClusterCoordinator _clusterCoordinator;
    private readonly IHealthMonitor _healthMonitor;
    private readonly ConcurrentDictionary<string, NodeAssignment> _currentAssignments;
    private readonly object _rebalanceLock = new();

    public LoadBalancer(
        ILogger<LoadBalancer> logger,
        IClusterCoordinator clusterCoordinator,
        IHealthMonitor healthMonitor)
    {
        _logger = logger;
        _clusterCoordinator = clusterCoordinator;
        _healthMonitor = healthMonitor;
        _currentAssignments = new ConcurrentDictionary<string, NodeAssignment>();
    }

    public async Task<NodeAssignment> GetAssignmentAsync(ValidationContext context)
    {
        var key = GetContextKey(context);
        if (_currentAssignments.TryGetValue(key, out var existing))
        {
            if (await _healthMonitor.IsNodeHealthy(existing.NodeId))
            {
                return existing;
            }
        }

        // No valid assignment found, select a new node
        return await SelectNodeAsync(context);
    }

    public async Task<NodeAssignment> SelectNodeAsync(ValidationContext context)
    {
        var healthyNodes = await _healthMonitor.GetClusterHealth();
        var candidates = healthyNodes
            .Where(n => n.IsHealthy && CanHandleValidation(n, context))
            .OrderBy(n => n.ResourceMetrics.CpuUsagePercent)
            .ThenBy(n => n.ResourceMetrics.MemoryUsageMB)
            .ToList();

        if (!candidates.Any())
        {
            throw new InvalidOperationException("No healthy nodes available to handle validation");
        }

        var selected = candidates.First();
        var assignment = new NodeAssignment
        {
            NodeId = selected.NodeId,
            LoadFactor = CalculateLoadFactor(selected.ResourceMetrics),
            SupportedLanguages = selected.ResourceMetrics.SupportedLanguages,
            CurrentResourceMetrics = selected.ResourceMetrics
        };

        var key = GetContextKey(context);
        _currentAssignments[key] = assignment;

        _logger.LogInformation("Selected node {NodeId} for validation {Key} with load factor {LoadFactor}", 
            assignment.NodeId, key, assignment.LoadFactor);

        return assignment;
    }

    public async Task RebalanceWorkload()
    {
        lock (_rebalanceLock)
        {
            try
            {
                _logger.LogInformation("Starting workload rebalancing");

                var healthyNodes = _healthMonitor.GetClusterHealth().Result;
                var currentLoad = _currentAssignments.GroupBy(x => x.Value.NodeId)
                    .ToDictionary(g => g.Key, g => g.Count());

                var avgLoad = currentLoad.Values.Average();
                var overloadedNodes = currentLoad
                    .Where(kv => kv.Value > avgLoad * 1.2) // 20% above average
                    .Select(kv => kv.Key)
                    .ToList();

                var underloadedNodes = healthyNodes
                    .Where(n => n.IsHealthy && (!currentLoad.ContainsKey(n.NodeId) || currentLoad[n.NodeId] < avgLoad * 0.8))
                    .Select(n => n.NodeId)
                    .ToList();

                foreach (var nodeId in overloadedNodes)
                {
                    var assignments = _currentAssignments
                        .Where(kv => kv.Value.NodeId == nodeId)
                        .ToList();

                    var excess = assignments.Count - (int)avgLoad;
                    var toMove = assignments.Take(excess).ToList();

                    foreach (var (key, assignment) in toMove)
                    {
                        if (underloadedNodes.Any())
                        {
                            var targetNode = underloadedNodes.First();
                            _currentAssignments[key] = new NodeAssignment
                            {
                                NodeId = targetNode,
                                LoadFactor = assignment.LoadFactor,
                                SupportedLanguages = assignment.SupportedLanguages,
                                CurrentResourceMetrics = healthyNodes.First(n => n.NodeId == targetNode).ResourceMetrics
                            };

                            _logger.LogInformation("Moved validation {Key} from {SourceNode} to {TargetNode}", 
                                key, nodeId, targetNode);

                            if (currentLoad.TryGetValue(targetNode, out var load))
                            {
                                currentLoad[targetNode] = load + 1;
                                if (load + 1 >= avgLoad * 0.8)
                                {
                                    underloadedNodes.Remove(targetNode);
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Workload rebalancing completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebalance workload");
                throw;
            }
        }
    }

    private string GetContextKey(ValidationContext context)
    {
        return $"{context.Language}:{ComputeCodeHash(context.Code)}";
    }

    private bool CanHandleValidation(NodeHealth node, ValidationContext context)
    {
        return node.ResourceMetrics.SupportedLanguages.Contains(context.Language) &&
               node.ResourceMetrics.HasAvailableCapacity;
    }

    private double CalculateLoadFactor(ResourceMetrics metrics)
    {
        return (metrics.CpuUsagePercent * 0.4) + 
               (metrics.MemoryUsageMB / metrics.TotalMemoryMB * 0.4) +
               (metrics.ActiveThreads / metrics.MaxThreads * 0.2);
    }

    private string ComputeCodeHash(string code)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hashBytes);
    }
}