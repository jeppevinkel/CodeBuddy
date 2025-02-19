using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Interfaces;
using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.Monitoring;

/// <summary>
/// Manages plugin resource allocation and reallocation
/// </summary>
public class PluginResourceManager
{
    private readonly ILogger<PluginResourceManager> _logger;
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly IPluginManager _pluginManager;
    private readonly ConcurrentDictionary<string, ResourceAllocation> _allocations;
    private readonly ConcurrentDictionary<string, List<ResourceSnapshot>> _resourceHistory;

    public PluginResourceManager(
        ILogger<PluginResourceManager> logger,
        PluginHealthMonitor healthMonitor,
        IPluginManager pluginManager)
    {
        _logger = logger;
        _healthMonitor = healthMonitor;
        _pluginManager = pluginManager;
        _allocations = new ConcurrentDictionary<string, ResourceAllocation>();
        _resourceHistory = new ConcurrentDictionary<string, List<ResourceSnapshot>>();
    }

    /// <summary>
    /// Allocates initial resources for a plugin
    /// </summary>
    public async Task<bool> AllocateResources(string pluginId, ResourceRequirements requirements)
    {
        try
        {
            _logger.LogInformation("Allocating resources for plugin {Id}", pluginId);

            var allocation = new ResourceAllocation
            {
                PluginId = pluginId,
                AllocatedAt = DateTime.UtcNow,
                Memory = requirements.MinMemoryMB,
                CpuCores = requirements.MinCpuCores,
                IoQuota = requirements.IoQuota,
                NetworkQuota = requirements.NetworkQuota
            };

            // Record baseline snapshot
            RecordResourceSnapshot(pluginId, allocation);

            // Apply allocation
            if (await ApplyResourceAllocation(pluginId, allocation))
            {
                _allocations[pluginId] = allocation;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate resources for plugin {Id}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Reallocates resources for a plugin based on current usage and health
    /// </summary>
    public async Task<bool> ReallocateResources(string pluginId)
    {
        try
        {
            var status = _healthMonitor.GetPluginHealth(pluginId);
            if (status == null) return false;

            _logger.LogInformation("Reallocating resources for plugin {Id}", pluginId);

            // Calculate new allocation based on usage patterns
            var currentAllocation = _allocations.GetValueOrDefault(pluginId);
            if (currentAllocation == null) return false;

            var newAllocation = CalculateOptimalAllocation(status, currentAllocation);

            // Record reallocation snapshot
            RecordResourceSnapshot(pluginId, newAllocation);

            // Apply new allocation
            if (await ApplyResourceAllocation(pluginId, newAllocation))
            {
                _allocations[pluginId] = newAllocation;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reallocate resources for plugin {Id}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Implements graceful degradation of resource usage
    /// </summary>
    public async Task<bool> DegradeResources(string pluginId, DegradationLevel level)
    {
        try
        {
            var currentAllocation = _allocations.GetValueOrDefault(pluginId);
            if (currentAllocation == null) return false;

            _logger.LogInformation("Degrading resources for plugin {Id} to level {Level}", 
                pluginId, level);

            var degradedAllocation = new ResourceAllocation
            {
                PluginId = pluginId,
                AllocatedAt = DateTime.UtcNow,
                Memory = CalculateDegradedMemory(currentAllocation.Memory, level),
                CpuCores = CalculateDegradedCpu(currentAllocation.CpuCores, level),
                IoQuota = CalculateDegradedIo(currentAllocation.IoQuota, level),
                NetworkQuota = CalculateDegradedNetwork(currentAllocation.NetworkQuota, level)
            };

            // Record degradation snapshot
            RecordResourceSnapshot(pluginId, degradedAllocation);

            // Apply degraded allocation
            if (await ApplyResourceAllocation(pluginId, degradedAllocation))
            {
                _allocations[pluginId] = degradedAllocation;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to degrade resources for plugin {Id}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Gets resource utilization history for a plugin
    /// </summary>
    public List<ResourceSnapshot> GetResourceHistory(string pluginId)
    {
        return _resourceHistory.GetValueOrDefault(pluginId) ?? new List<ResourceSnapshot>();
    }

    private void RecordResourceSnapshot(string pluginId, ResourceAllocation allocation)
    {
        if (!_resourceHistory.ContainsKey(pluginId))
        {
            _resourceHistory[pluginId] = new List<ResourceSnapshot>();
        }

        var snapshot = new ResourceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Allocation = allocation,
            HealthStatus = _healthMonitor.GetPluginHealth(pluginId)
        };

        _resourceHistory[pluginId].Add(snapshot);

        // Keep only last 24 hours of history
        var cutoff = DateTime.UtcNow.AddHours(-24);
        _resourceHistory[pluginId].RemoveAll(s => s.Timestamp < cutoff);
    }

    private ResourceAllocation CalculateOptimalAllocation(
        PluginHealthStatus status, 
        ResourceAllocation currentAllocation)
    {
        var memoryUtilization = status.MemoryUsage / (currentAllocation.Memory * 1024 * 1024);
        var cpuUtilization = status.CpuUsage / (currentAllocation.CpuCores * 100);

        return new ResourceAllocation
        {
            PluginId = currentAllocation.PluginId,
            AllocatedAt = DateTime.UtcNow,
            Memory = CalculateOptimalMemory(currentAllocation.Memory, memoryUtilization),
            CpuCores = CalculateOptimalCpu(currentAllocation.CpuCores, cpuUtilization),
            IoQuota = currentAllocation.IoQuota, // Maintain current IO quota
            NetworkQuota = currentAllocation.NetworkQuota // Maintain current network quota
        };
    }

    private async Task<bool> ApplyResourceAllocation(string pluginId, ResourceAllocation allocation)
    {
        try
        {
            // Integration point: Apply resource limits through container/runtime system
            _logger.LogInformation(
                "Applied resource allocation for plugin {Id}: Memory={Memory}MB, CPU={Cpu} cores",
                pluginId, allocation.Memory, allocation.CpuCores);
            
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply resource allocation for plugin {Id}", pluginId);
            return false;
        }
    }

    private int CalculateOptimalMemory(int currentMemory, double utilization)
    {
        if (utilization > 0.9) return (int)(currentMemory * 1.2); // Increase by 20%
        if (utilization < 0.6) return (int)(currentMemory * 0.8); // Decrease by 20%
        return currentMemory;
    }

    private double CalculateOptimalCpu(double currentCpu, double utilization)
    {
        if (utilization > 0.9) return currentCpu * 1.2; // Increase by 20%
        if (utilization < 0.6) return currentCpu * 0.8; // Decrease by 20%
        return currentCpu;
    }

    private int CalculateDegradedMemory(int currentMemory, DegradationLevel level) =>
        level switch
        {
            DegradationLevel.Light => (int)(currentMemory * 0.8),
            DegradationLevel.Moderate => (int)(currentMemory * 0.6),
            DegradationLevel.Severe => (int)(currentMemory * 0.4),
            _ => currentMemory
        };

    private double CalculateDegradedCpu(double currentCpu, DegradationLevel level) =>
        level switch
        {
            DegradationLevel.Light => currentCpu * 0.8,
            DegradationLevel.Moderate => currentCpu * 0.6,
            DegradationLevel.Severe => currentCpu * 0.4,
            _ => currentCpu
        };

    private int CalculateDegradedIo(int currentIo, DegradationLevel level) =>
        level switch
        {
            DegradationLevel.Light => (int)(currentIo * 0.8),
            DegradationLevel.Moderate => (int)(currentIo * 0.6),
            DegradationLevel.Severe => (int)(currentIo * 0.4),
            _ => currentIo
        };

    private int CalculateDegradedNetwork(int currentNetwork, DegradationLevel level) =>
        level switch
        {
            DegradationLevel.Light => (int)(currentNetwork * 0.8),
            DegradationLevel.Moderate => (int)(currentNetwork * 0.6),
            DegradationLevel.Severe => (int)(currentNetwork * 0.4),
            _ => currentNetwork
        };
}

public class ResourceAllocation
{
    public string PluginId { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }
    public int Memory { get; set; } // in MB
    public double CpuCores { get; set; }
    public int IoQuota { get; set; } // operations per second
    public int NetworkQuota { get; set; } // KB per second
}

public class ResourceRequirements
{
    public int MinMemoryMB { get; set; }
    public double MinCpuCores { get; set; }
    public int IoQuota { get; set; }
    public int NetworkQuota { get; set; }
}

public class ResourceSnapshot
{
    public DateTime Timestamp { get; set; }
    public ResourceAllocation Allocation { get; set; } = new();
    public PluginHealthStatus? HealthStatus { get; set; }
}

public enum DegradationLevel
{
    Light,
    Moderate,
    Severe
}