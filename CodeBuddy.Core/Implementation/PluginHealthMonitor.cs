using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using CodeBuddy.Core.Interfaces;
using System.Linq;

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
    private readonly ConcurrentDictionary<string, Queue<long>> _memoryHistory;
    private readonly ConcurrentDictionary<string, Queue<double>> _responseTimeHistory;
    private readonly ConcurrentDictionary<string, int> _resourceLeakCounts;
    private readonly Dictionary<string, List<double>> _trendData;
    private readonly Timer _monitoringTimer;
    private readonly Timer _trendsAnalysisTimer;
    private readonly Timer _configValidationTimer;
    private bool _disposed;
    
    // Constants for monitoring thresholds
    private const int MemoryThresholdMB = 500;
    private const double CpuThresholdPercent = 80;
    private const double ResponseTimeThresholdMs = 1000;
    private const int HistoryQueueSize = 60; // Keep last 60 readings
    private const double TrendThresholdPercent = 10;

    public PluginHealthMonitor(ILogger<PluginHealthMonitor> logger)
    {
        _logger = logger;
        _healthStatuses = new ConcurrentDictionary<string, PluginHealthStatus>();
        _pluginProcesses = new ConcurrentDictionary<string, Process>();
        _initializationTimes = new ConcurrentDictionary<string, DateTimeOffset>();
        _memoryHistory = new ConcurrentDictionary<string, Queue<long>>();
        _responseTimeHistory = new ConcurrentDictionary<string, Queue<double>>();
        _resourceLeakCounts = new ConcurrentDictionary<string, int>();
        _trendData = new Dictionary<string, List<double>>();

        // Initialize timers for different monitoring aspects
        _monitoringTimer = new Timer(MonitorPlugins, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        _trendsAnalysisTimer = new Timer(AnalyzeTrends, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        _configValidationTimer = new Timer(ValidateConfigurations, null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
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

                    // Update memory history
                    if (!_memoryHistory.ContainsKey(pluginId))
                    {
                        _memoryHistory[pluginId] = new Queue<long>(HistoryQueueSize);
                    }
                    var memoryQueue = _memoryHistory[pluginId];
                    if (memoryQueue.Count >= HistoryQueueSize)
                    {
                        memoryQueue.Dequeue();
                    }
                    memoryQueue.Enqueue(healthStatus.MemoryUsage);

                    // Detect memory leaks
                    var memoryGrowthRate = CalculateMemoryGrowthRate(memoryQueue.ToList());
                    healthStatus.LeakMetrics.MemoryGrowthRate = memoryGrowthRate;
                    if (memoryGrowthRate > 0)
                    {
                        healthStatus.LeakMetrics.MemoryLeakCount++;
                        healthStatus.Warnings.ResourceWarnings.Add($"Potential memory leak detected: {memoryGrowthRate:F2} bytes/hour growth rate");
                    }

                    // Update plugin state and warnings based on resource usage
                    UpdatePluginState(pluginId, healthStatus);
                    UpdateEarlyWarnings(healthStatus);

                    // Update trends
                    UpdateResourceTrends(healthStatus, memoryQueue.ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring plugin: {Id}", pluginId);
                RecordError(pluginId, ex, true);
            }
        }
    }

    private void UpdatePluginState(string pluginId, PluginHealthStatus status)
    {
        var newState = status.State;
        var warningCount = status.Warnings.ResourceWarnings.Count + 
                          status.Warnings.PerformanceWarnings.Count + 
                          status.Warnings.ConfigurationWarnings.Count;

        if (status.UnrecoverableErrorCount > 0)
        {
            newState = PluginState.Failed;
        }
        else if (status.CpuUsage > CpuThresholdPercent || 
                 status.MemoryUsage > MemoryThresholdMB * 1024 * 1024 ||
                 warningCount > 5)
        {
            newState = PluginState.Degraded;
        }
        else if (status.State == PluginState.Degraded && 
                 status.CpuUsage < CpuThresholdPercent && 
                 status.MemoryUsage < MemoryThresholdMB * 1024 * 1024 &&
                 warningCount <= 2)
        {
            newState = PluginState.Running;
        }

        if (newState != status.State)
        {
            UpdatePluginState(pluginId, newState);
        }
    }

    private void UpdateEarlyWarnings(PluginHealthStatus status)
    {
        status.Warnings = new EarlyWarningStatus();
        var severity = WarningSeverity.None;

        // Resource warnings
        if (status.MemoryUsage > MemoryThresholdMB * 1024 * 1024 * 0.8)
        {
            status.Warnings.ResourceWarnings.Add($"High memory usage: {status.MemoryUsage / (1024*1024):F2}MB");
            severity = WarningSeverity.Medium;
        }
        if (status.CpuUsage > CpuThresholdPercent * 0.8)
        {
            status.Warnings.ResourceWarnings.Add($"High CPU usage: {status.CpuUsage:F2}%");
            severity = WarningSeverity.Medium;
        }

        // Performance warnings
        foreach (var (operation, responseTime) in status.ResponseTimes)
        {
            if (responseTime > ResponseTimeThresholdMs)
            {
                status.Warnings.PerformanceWarnings.Add($"Slow operation ({operation}): {responseTime:F2}ms");
                severity = severity < WarningSeverity.High ? WarningSeverity.High : severity;
            }
        }

        // Configuration warnings
        if (!status.ConfigStatus.IsValid)
        {
            status.Warnings.ConfigurationWarnings.AddRange(status.ConfigStatus.ValidationErrors);
            severity = WarningSeverity.High;
        }

        status.Warnings.Severity = severity;
    }

    private void AnalyzeTrends(object? state)
    {
        foreach (var (pluginId, healthStatus) in _healthStatuses)
        {
            try
            {
                if (_memoryHistory.TryGetValue(pluginId, out var memoryQueue))
                {
                    var memoryList = memoryQueue.ToList();
                    if (memoryList.Count >= 2)
                    {
                        var memoryTrend = CalculateTrendPercentage(memoryList);
                        healthStatus.Trends.MemoryTrend = memoryTrend;

                        if (Math.Abs(memoryTrend) > TrendThresholdPercent)
                        {
                            var direction = memoryTrend > 0 ? "increasing" : "decreasing";
                            healthStatus.Warnings.ResourceWarnings.Add(
                                $"Memory usage trend {direction} by {Math.Abs(memoryTrend):F2}%");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing trends for plugin: {Id}", pluginId);
            }
        }
    }

    private void ValidateConfigurations(object? state)
    {
        foreach (var (pluginId, healthStatus) in _healthStatuses)
        {
            try
            {
                // Perform configuration validation
                healthStatus.ConfigStatus.LastValidated = DateTimeOffset.UtcNow;
                // TODO: Implement actual configuration validation logic
                
                // Check for configuration drift
                // TODO: Implement configuration drift detection
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration for plugin: {Id}", pluginId);
            }
        }
    }

    private double CalculateMemoryGrowthRate(List<long> memoryHistory)
    {
        if (memoryHistory.Count < 2) return 0;

        var timePeriodHours = (double)HistoryQueueSize * 10 / 3600; // 10 seconds between readings
        var memoryGrowth = memoryHistory[^1] - memoryHistory[0];
        return memoryGrowth / timePeriodHours;
    }

    private double CalculateTrendPercentage(List<double> values)
    {
        if (values.Count < 2) return 0;
        var oldValue = values[0];
        var newValue = values[^1];
        return (newValue - oldValue) / oldValue * 100;
    }

    private void UpdateResourceTrends(PluginHealthStatus status, List<long> memoryHistory)
    {
        if (memoryHistory.Count < 2) return;

        status.Trends.MemoryTrend = CalculateTrendPercentage(memoryHistory.Select(m => (double)m).ToList());
        
        // Calculate other trends if data is available
        if (status.ResponseTimes.Any())
        {
            status.Trends.ResponseTimeTrend = CalculateTrendPercentage(
                status.ResponseTimes.Values.ToList());
        }

        var errorRate = (double)status.Errors.Count / status.Uptime.TotalHours;
        if (!_trendData.ContainsKey(status.PluginId))
        {
            _trendData[status.PluginId] = new List<double>();
        }
        _trendData[status.PluginId].Add(errorRate);
        if (_trendData[status.PluginId].Count > 2)
        {
            status.Trends.ErrorRateTrend = CalculateTrendPercentage(_trendData[status.PluginId]);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _monitoringTimer.Dispose();
        _trendsAnalysisTimer.Dispose();
        _configValidationTimer.Dispose();

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
        _memoryHistory.Clear();
        _responseTimeHistory.Clear();
        _resourceLeakCounts.Clear();
        _trendData.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Records response time for a plugin operation
    /// </summary>
    public void RecordResponseTime(string pluginId, string operation, double milliseconds)
    {
        if (_healthStatuses.TryGetValue(pluginId, out var status))
        {
            status.ResponseTimes[operation] = milliseconds;

            if (!_responseTimeHistory.ContainsKey(pluginId))
            {
                _responseTimeHistory[pluginId] = new Queue<double>(HistoryQueueSize);
            }
            var responseQueue = _responseTimeHistory[pluginId];
            if (responseQueue.Count >= HistoryQueueSize)
            {
                responseQueue.Dequeue();
            }
            responseQueue.Enqueue(milliseconds);

            if (milliseconds > ResponseTimeThresholdMs)
            {
                status.Warnings.PerformanceWarnings.Add(
                    $"Operation {operation} exceeded response time threshold: {milliseconds:F2}ms");
            }
        }
    }

    /// <summary>
    /// Records a successful resource cleanup
    /// </summary>
    public void RecordResourceCleanup(string pluginId, bool success)
    {
        if (_healthStatuses.TryGetValue(pluginId, out var status))
        {
            var totalCleanups = status.LeakMetrics.CleanupSuccessRate * status.RecoveryCount + (success ? 1 : 0);
            status.RecoveryCount++;
            status.LeakMetrics.CleanupSuccessRate = totalCleanups / status.RecoveryCount * 100;
        }
    }

    /// <summary>
    /// Records a handle leak detection
    /// </summary>
    public void RecordHandleLeak(string pluginId)
    {
        if (_healthStatuses.TryGetValue(pluginId, out var status))
        {
            status.LeakMetrics.HandleLeakCount++;
            status.Warnings.ResourceWarnings.Add("Resource handle leak detected");
            UpdatePluginState(pluginId, PluginState.Degraded);
        }
    }
}