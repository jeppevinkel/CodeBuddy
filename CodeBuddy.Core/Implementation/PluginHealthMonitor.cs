using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Monitors plugin health and resource usage
/// </summary>
public class PluginHealthMonitor : IDisposable
{
    private readonly ILogger<PluginHealthMonitor> _logger;
    private readonly ConcurrentDictionary<string, PluginHealthStatus> _healthStatuses;
    private readonly ConcurrentDictionary<string, Process> _pluginProcesses;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _initializationTimes;
    private readonly Timer _monitoringTimer;
    private bool _disposed;

    public PluginHealthMonitor(ILogger<PluginHealthMonitor> logger)
    {
        _logger = logger;
        _healthStatuses = new ConcurrentDictionary<string, PluginHealthStatus>();
        _pluginProcesses = new ConcurrentDictionary<string, Process>();
        _initializationTimes = new ConcurrentDictionary<string, DateTimeOffset>();
        _monitoringTimer = new Timer(MonitorPlugins, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Registers a plugin for health monitoring
    /// </summary>
    public void RegisterPlugin(IPlugin plugin)
    {
        var status = new PluginHealthStatus
        {
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            Version = plugin.Version,
            State = PluginState.Initializing,
            LastStateChange = DateTimeOffset.UtcNow,
            LoadedDependencies = new List<string>(plugin.Dependencies.Select(d => d.PluginId))
        };

        _healthStatuses.TryAdd(plugin.Id, status);
        _initializationTimes.TryAdd(plugin.Id, DateTimeOffset.UtcNow);

        try
        {
            var currentProcess = Process.GetCurrentProcess();
            _pluginProcesses.TryAdd(plugin.Id, currentProcess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering plugin process: {Id}", plugin.Id);
        }
    }

    /// <summary>
    /// Updates plugin state
    /// </summary>
    public void UpdatePluginState(string pluginId, PluginState newState)
    {
        if (_healthStatuses.TryGetValue(pluginId, out var status))
        {
            status.State = newState;
            status.LastStateChange = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Records a plugin error
    /// </summary>
    public void RecordError(string pluginId, Exception error, bool isRecoverable)
    {
        if (_healthStatuses.TryGetValue(pluginId, out var status))
        {
            status.Errors.Add(error.Message);
            if (isRecoverable)
            {
                status.RecoveryCount++;
            }
            else
            {
                status.UnrecoverableErrorCount++;
                status.State = PluginState.Failed;
            }
            status.LastStateChange = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Gets the current health status of a plugin
    /// </summary>
    public PluginHealthStatus? GetPluginHealth(string pluginId)
    {
        return _healthStatuses.TryGetValue(pluginId, out var status) ? status : null;
    }

    /// <summary>
    /// Gets health status for all monitored plugins
    /// </summary>
    public IEnumerable<PluginHealthStatus> GetAllPluginsHealth()
    {
        return _healthStatuses.Values;
    }

    private void MonitorPlugins(object? state)
    {
        foreach (var (pluginId, healthStatus) in _healthStatuses)
        {
            try
            {
                if (_pluginProcesses.TryGetValue(pluginId, out var process))
                {
                    var processStartTime = _initializationTimes[pluginId];
                    healthStatus.Uptime = DateTimeOffset.UtcNow - processStartTime;

                    process.Refresh();
                    healthStatus.MemoryUsage = process.WorkingSet64;
                    healthStatus.CpuUsage = process.TotalProcessorTime.TotalMilliseconds / 
                                          (Environment.ProcessorCount * healthStatus.Uptime.TotalMilliseconds) * 100;

                    // Update state based on resource usage
                    if (healthStatus.State == PluginState.Running || 
                        healthStatus.State == PluginState.Degraded)
                    {
                        if (healthStatus.CpuUsage > 80 || healthStatus.MemoryUsage > 500_000_000) // 500MB
                        {
                            UpdatePluginState(pluginId, PluginState.Degraded);
                        }
                        else if (healthStatus.State == PluginState.Degraded)
                        {
                            UpdatePluginState(pluginId, PluginState.Running);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring plugin: {Id}", pluginId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _monitoringTimer.Dispose();
        foreach (var process in _pluginProcesses.Values)
        {
            try
            {
                process.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
        }

        _pluginProcesses.Clear();
        _healthStatuses.Clear();
        _initializationTimes.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}