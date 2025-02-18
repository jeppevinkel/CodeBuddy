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