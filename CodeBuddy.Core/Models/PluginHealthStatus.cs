using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Represents the health status of a plugin
/// </summary>
public class PluginHealthStatus
{
    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Plugin display name
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Current plugin state
    /// </summary>
    public PluginState State { get; set; } = PluginState.Unknown;

    /// <summary>
    /// Timestamp of the last state change
    /// </summary>
    public DateTimeOffset LastStateChange { get; set; }

    /// <summary>
    /// Current plugin memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Current plugin CPU usage percentage
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// List of active error messages
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Count of successful recoveries from errors
    /// </summary>
    public int RecoveryCount { get; set; }

    /// <summary>
    /// Count of unrecoverable errors
    /// </summary>
    public int UnrecoverableErrorCount { get; set; }

    /// <summary>
    /// Uptime since last initialization
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Plugin version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Loaded dependencies
    /// </summary>
    public List<string> LoadedDependencies { get; set; } = new();

    /// <summary>
    /// Response times in milliseconds for plugin operations
    /// </summary>
    public Dictionary<string, double> ResponseTimes { get; set; } = new();

    /// <summary>
    /// Resource leak indicators
    /// </summary>
    public ResourceLeakMetrics LeakMetrics { get; set; } = new();

    /// <summary>
    /// Configuration validation status
    /// </summary>
    public ConfigurationHealthStatus ConfigStatus { get; set; } = new();

    /// <summary>
    /// Resource trend analysis
    /// </summary>
    public ResourceTrends Trends { get; set; } = new();

    /// <summary>
    /// Early warning indicators
    /// </summary>
    public EarlyWarningStatus Warnings { get; set; } = new();
}

/// <summary>
/// Represents resource leak metrics
/// </summary>
public class ResourceLeakMetrics
{
    /// <summary>
    /// Number of detected memory leaks
    /// </summary>
    public int MemoryLeakCount { get; set; }

    /// <summary>
    /// Number of detected resource handle leaks
    /// </summary>
    public int HandleLeakCount { get; set; }

    /// <summary>
    /// Memory growth rate (bytes per hour)
    /// </summary>
    public double MemoryGrowthRate { get; set; }

    /// <summary>
    /// Resource cleanup success rate (percentage)
    /// </summary>
    public double CleanupSuccessRate { get; set; }
}

/// <summary>
/// Represents configuration health status
/// </summary>
public class ConfigurationHealthStatus
{
    /// <summary>
    /// Whether the configuration is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of configuration validation errors
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Last successful configuration validation timestamp
    /// </summary>
    public DateTimeOffset LastValidated { get; set; }

    /// <summary>
    /// Configuration drift detection status
    /// </summary>
    public bool HasConfigurationDrift { get; set; }
}

/// <summary>
/// Represents resource usage trends
/// </summary>
public class ResourceTrends
{
    /// <summary>
    /// Memory usage trend (percentage change)
    /// </summary>
    public double MemoryTrend { get; set; }

    /// <summary>
    /// CPU usage trend (percentage change)
    /// </summary>
    public double CpuTrend { get; set; }

    /// <summary>
    /// Response time trend (percentage change)
    /// </summary>
    public double ResponseTimeTrend { get; set; }

    /// <summary>
    /// Error rate trend (percentage change)
    /// </summary>
    public double ErrorRateTrend { get; set; }
}

/// <summary>
/// Represents early warning status
/// </summary>
public class EarlyWarningStatus
{
    /// <summary>
    /// Resource exhaustion warnings
    /// </summary>
    public List<string> ResourceWarnings { get; set; } = new();

    /// <summary>
    /// Performance degradation warnings
    /// </summary>
    public List<string> PerformanceWarnings { get; set; } = new();

    /// <summary>
    /// Configuration warnings
    /// </summary>
    public List<string> ConfigurationWarnings { get; set; } = new();

    /// <summary>
    /// Warning severity level
    /// </summary>
    public WarningSeverity Severity { get; set; }
}

/// <summary>
/// Represents warning severity levels
/// </summary>
public enum WarningSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents possible plugin states
/// </summary>
public enum PluginState
{
    Unknown,
    Initializing,
    Running,
    Degraded,
    Failed,
    Disabled,
    Reloading
}