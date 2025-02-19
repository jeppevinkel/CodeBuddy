using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.Monitoring;

/// <summary>
/// Provides a dashboard for monitoring plugin health and system status
/// </summary>
public class PluginHealthDashboard
{
    private readonly PluginHealthMonitor _healthMonitor;
    private readonly ILogger<PluginHealthDashboard> _logger;
    private readonly Dictionary<string, List<PerformanceMetrics>> _historicalMetrics;

    public PluginHealthDashboard(
        PluginHealthMonitor healthMonitor,
        ILogger<PluginHealthDashboard> logger)
    {
        _healthMonitor = healthMonitor;
        _logger = logger;
        _historicalMetrics = new Dictionary<string, List<PerformanceMetrics>>();
    }

    /// <summary>
    /// Gets the current system-wide health status
    /// </summary>
    public SystemHealthStatus GetSystemStatus()
    {
        var pluginStatuses = _healthMonitor.GetAllPluginsHealth().ToList();
        
        return new SystemHealthStatus
        {
            TotalPlugins = pluginStatuses.Count,
            HealthyPlugins = pluginStatuses.Count(p => p.State == PluginState.Running),
            DegradedPlugins = pluginStatuses.Count(p => p.State == PluginState.Degraded),
            FailedPlugins = pluginStatuses.Count(p => p.State == PluginState.Failed),
            SystemWideWarnings = GetSystemWideWarnings(pluginStatuses),
            OverallHealth = CalculateOverallHealth(pluginStatuses),
            TopResourceConsumers = GetTopResourceConsumers(pluginStatuses),
            ResourceUtilization = GetResourceUtilization(pluginStatuses)
        };
    }

    /// <summary>
    /// Gets detailed health metrics for a specific plugin
    /// </summary>
    public PluginDetailedHealth GetPluginDetailedHealth(string pluginId)
    {
        var status = _healthMonitor.GetPluginHealth(pluginId);
        if (status == null)
        {
            throw new KeyNotFoundException($"Plugin {pluginId} not found");
        }

        return new PluginDetailedHealth
        {
            PluginId = status.PluginId,
            PluginName = status.PluginName,
            Version = status.Version,
            State = status.State,
            Uptime = status.Uptime,
            MemoryUsage = status.MemoryUsage,
            CpuUsage = status.CpuUsage,
            ResponseTimes = status.ResponseTimes,
            LeakMetrics = status.LeakMetrics,
            ConfigurationStatus = status.ConfigStatus,
            ResourceTrends = status.Trends,
            Warnings = status.Warnings,
            ErrorHistory = status.Errors,
            RecoveryRate = CalculateRecoveryRate(status),
            PerformanceHistory = GetPerformanceHistory(pluginId)
        };
    }

    /// <summary>
    /// Records performance metrics for historical tracking
    /// </summary>
    public void RecordMetrics(string pluginId, PerformanceMetrics metrics)
    {
        if (!_historicalMetrics.ContainsKey(pluginId))
        {
            _historicalMetrics[pluginId] = new List<PerformanceMetrics>();
        }

        _historicalMetrics[pluginId].Add(metrics);

        // Keep only last 24 hours of metrics
        var cutoff = DateTime.UtcNow.AddHours(-24);
        _historicalMetrics[pluginId] = _historicalMetrics[pluginId]
            .Where(m => m.Timestamp >= cutoff)
            .ToList();
    }

    private List<string> GetSystemWideWarnings(List<PluginHealthStatus> statuses)
    {
        var warnings = new List<string>();

        var totalMemory = statuses.Sum(s => s.MemoryUsage);
        var totalCpu = statuses.Sum(s => s.CpuUsage);
        var degradedCount = statuses.Count(s => s.State == PluginState.Degraded);

        if (totalMemory > 1024L * 1024L * 1024L * 2L) // 2GB
        {
            warnings.Add($"High total memory usage: {totalMemory / (1024*1024):F2}MB");
        }

        if (totalCpu > 150) // Over 150% CPU (multi-core)
        {
            warnings.Add($"High total CPU usage: {totalCpu:F2}%");
        }

        if (degradedCount > statuses.Count * 0.2) // More than 20% degraded
        {
            warnings.Add($"High number of degraded plugins: {degradedCount} out of {statuses.Count}");
        }

        return warnings;
    }

    private SystemHealth CalculateOverallHealth(List<PluginHealthStatus> statuses)
    {
        if (!statuses.Any()) return SystemHealth.Unknown;

        var failedPercentage = (double)statuses.Count(s => s.State == PluginState.Failed) / statuses.Count;
        var degradedPercentage = (double)statuses.Count(s => s.State == PluginState.Degraded) / statuses.Count;

        if (failedPercentage > 0.1) return SystemHealth.Critical;
        if (failedPercentage > 0.05 || degradedPercentage > 0.2) return SystemHealth.Poor;
        if (degradedPercentage > 0.1) return SystemHealth.Fair;
        return SystemHealth.Good;
    }

    private List<ResourceConsumer> GetTopResourceConsumers(List<PluginHealthStatus> statuses)
    {
        return statuses
            .Select(s => new ResourceConsumer
            {
                PluginId = s.PluginId,
                PluginName = s.PluginName,
                MemoryUsageMB = s.MemoryUsage / (1024 * 1024),
                CpuPercentage = s.CpuUsage
            })
            .OrderByDescending(r => r.MemoryUsageMB)
            .Take(5)
            .ToList();
    }

    private ResourceUtilization GetResourceUtilization(List<PluginHealthStatus> statuses)
    {
        return new ResourceUtilization
        {
            TotalMemoryUsageMB = statuses.Sum(s => s.MemoryUsage) / (1024 * 1024),
            TotalCpuPercentage = statuses.Sum(s => s.CpuUsage),
            AverageResponseTimeMs = statuses
                .SelectMany(s => s.ResponseTimes.Values)
                .DefaultIfEmpty()
                .Average()
        };
    }

    private double CalculateRecoveryRate(PluginHealthStatus status)
    {
        if (status.RecoveryCount + status.UnrecoverableErrorCount == 0) return 100;
        return (double)status.RecoveryCount / (status.RecoveryCount + status.UnrecoverableErrorCount) * 100;
    }

    private List<PerformanceMetrics> GetPerformanceHistory(string pluginId)
    {
        return _historicalMetrics.TryGetValue(pluginId, out var metrics) 
            ? metrics 
            : new List<PerformanceMetrics>();
    }
}

/// <summary>
/// Represents the overall system health status
/// </summary>
public class SystemHealthStatus
{
    public int TotalPlugins { get; set; }
    public int HealthyPlugins { get; set; }
    public int DegradedPlugins { get; set; }
    public int FailedPlugins { get; set; }
    public List<string> SystemWideWarnings { get; set; } = new();
    public SystemHealth OverallHealth { get; set; }
    public List<ResourceConsumer> TopResourceConsumers { get; set; } = new();
    public ResourceUtilization ResourceUtilization { get; set; } = new();
}

/// <summary>
/// Represents detailed health information for a specific plugin
/// </summary>
public class PluginDetailedHealth
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public PluginState State { get; set; }
    public TimeSpan Uptime { get; set; }
    public long MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
    public Dictionary<string, double> ResponseTimes { get; set; } = new();
    public ResourceLeakMetrics LeakMetrics { get; set; } = new();
    public ConfigurationHealthStatus ConfigurationStatus { get; set; } = new();
    public ResourceTrends ResourceTrends { get; set; } = new();
    public EarlyWarningStatus Warnings { get; set; } = new();
    public List<string> ErrorHistory { get; set; } = new();
    public double RecoveryRate { get; set; }
    public List<PerformanceMetrics> PerformanceHistory { get; set; } = new();
}

/// <summary>
/// Represents a top resource consuming plugin
/// </summary>
public class ResourceConsumer
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public double MemoryUsageMB { get; set; }
    public double CpuPercentage { get; set; }
}

/// <summary>
/// Represents overall resource utilization
/// </summary>
public class ResourceUtilization
{
    public double TotalMemoryUsageMB { get; set; }
    public double TotalCpuPercentage { get; set; }
    public double AverageResponseTimeMs { get; set; }
}

/// <summary>
/// Represents the overall system health state
/// </summary>
public enum SystemHealth
{
    Unknown,
    Critical,
    Poor,
    Fair,
    Good
}