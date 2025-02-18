using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ResourceMonitoringDashboard : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<ResourceMetricsModel> _metricsHistory;
        private readonly ConcurrentDictionary<string, ResourceAlert> _activeAlerts;
        private readonly ResourceThresholds _thresholds;
        private readonly Timer _monitoringTimer;
        private readonly int _maxHistoryItems = 1000;
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(1);
        private bool _disposed;

        public ResourceMonitoringDashboard(ILogger logger, ResourceThresholds thresholds = null)
        {
            _logger = logger;
            _metricsHistory = new ConcurrentQueue<ResourceMetricsModel>();
            _activeAlerts = new ConcurrentDictionary<string, ResourceAlert>();
            _thresholds = thresholds ?? new ResourceThresholds();
            _monitoringTimer = new Timer(MonitorResources, null, _monitoringInterval, _monitoringInterval);
        }

        public async Task<ResourceMetricsModel> GetCurrentMetricsAsync()
        {
            var process = Process.GetCurrentProcess();
            var metrics = new ResourceMetricsModel
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageBytes = process.WorkingSet64,
                CpuUsagePercent = await GetCpuUsageAsync(),
                ActiveHandles = process.HandleCount,
                HealthStatus = CalculateHealthStatus()
            };

            // Add to history and trim if needed
            _metricsHistory.Enqueue(metrics);
            while (_metricsHistory.Count > _maxHistoryItems)
            {
                _metricsHistory.TryDequeue(out _);
            }

            CheckThresholds(metrics);
            return metrics;
        }

        public ResourceTrendData GetTrendData(TimeSpan timeSpan)
        {
            var cutoffTime = DateTime.UtcNow - timeSpan;
            var relevantMetrics = _metricsHistory
                .Where(m => m.Timestamp >= cutoffTime)
                .OrderBy(m => m.Timestamp)
                .ToList();

            return new ResourceTrendData
            {
                StartTime = cutoffTime,
                EndTime = DateTime.UtcNow,
                Metrics = relevantMetrics,
                AverageUtilization = CalculateAverageUtilization(relevantMetrics),
                PeakUtilization = CalculatePeakUtilization(relevantMetrics),
                Alerts = _activeAlerts.Values.ToList()
            };
        }

        public IEnumerable<ResourceAlert> GetActiveAlerts()
        {
            return _activeAlerts.Values;
        }

        private async Task<double> GetCpuUsageAsync()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            await Task.Delay(100); // Sample over 100ms
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;
            return Math.Min(100, cpuUsageTotal); // Cap at 100%
        }

        private void MonitorResources(object state)
        {
            if (_disposed) return;

            try
            {
                var _ = GetCurrentMetricsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring resources");
            }
        }

        private void CheckThresholds(ResourceMetricsModel metrics)
        {
            CheckMemoryThreshold(metrics);
            CheckCpuThreshold(metrics);
            CheckHandleThreshold(metrics);
            CheckQueueSaturation(metrics);
        }

        private void CheckMemoryThreshold(ResourceMetricsModel metrics)
        {
            if (metrics.MemoryUsageBytes >= _thresholds.MemoryCriticalThresholdBytes)
            {
                RaiseAlert("MEM001", "Memory", "Critical", 
                    "Memory usage exceeds critical threshold",
                    new Dictionary<string, double>
                    {
                        ["CurrentUsageMB"] = metrics.MemoryUsageBytes / (1024.0 * 1024.0),
                        ["ThresholdMB"] = _thresholds.MemoryCriticalThresholdBytes / (1024.0 * 1024.0)
                    });
            }
            else if (metrics.MemoryUsageBytes >= _thresholds.MemoryWarningThresholdBytes)
            {
                RaiseAlert("MEM002", "Memory", "Warning",
                    "Memory usage exceeds warning threshold",
                    new Dictionary<string, double>
                    {
                        ["CurrentUsageMB"] = metrics.MemoryUsageBytes / (1024.0 * 1024.0),
                        ["ThresholdMB"] = _thresholds.MemoryWarningThresholdBytes / (1024.0 * 1024.0)
                    });
            }
            else
            {
                ClearAlert("MEM001");
                ClearAlert("MEM002");
            }
        }

        private void CheckCpuThreshold(ResourceMetricsModel metrics)
        {
            if (metrics.CpuUsagePercent >= _thresholds.CpuCriticalThresholdPercent)
            {
                RaiseAlert("CPU001", "CPU", "Critical",
                    "CPU usage exceeds critical threshold",
                    new Dictionary<string, double>
                    {
                        ["CurrentUsage"] = metrics.CpuUsagePercent,
                        ["Threshold"] = _thresholds.CpuCriticalThresholdPercent
                    });
            }
            else if (metrics.CpuUsagePercent >= _thresholds.CpuWarningThresholdPercent)
            {
                RaiseAlert("CPU002", "CPU", "Warning",
                    "CPU usage exceeds warning threshold",
                    new Dictionary<string, double>
                    {
                        ["CurrentUsage"] = metrics.CpuUsagePercent,
                        ["Threshold"] = _thresholds.CpuWarningThresholdPercent
                    });
            }
            else
            {
                ClearAlert("CPU001");
                ClearAlert("CPU002");
            }
        }

        private void CheckHandleThreshold(ResourceMetricsModel metrics)
        {
            if (metrics.ActiveHandles > _thresholds.MaxHandleCount)
            {
                RaiseAlert("HDL001", "Handles", "Warning",
                    "Handle count exceeds threshold",
                    new Dictionary<string, double>
                    {
                        ["CurrentHandles"] = metrics.ActiveHandles,
                        ["Threshold"] = _thresholds.MaxHandleCount
                    });
            }
            else
            {
                ClearAlert("HDL001");
            }
        }

        private void CheckQueueSaturation(ResourceMetricsModel metrics)
        {
            if (metrics.ValidationQueueMetrics.TryGetValue("QueueUtilization", out var utilization))
            {
                if (utilization > _thresholds.QueueSaturationThreshold)
                {
                    RaiseAlert("QUE001", "Queue", "Warning",
                        "Validation queue approaching saturation",
                        new Dictionary<string, double>
                        {
                            ["CurrentUtilization"] = utilization,
                            ["Threshold"] = _thresholds.QueueSaturationThreshold
                        });
                }
                else
                {
                    ClearAlert("QUE001");
                }
            }
        }

        private void RaiseAlert(string alertId, string resourceType, string severity, string message,
            Dictionary<string, double> metrics)
        {
            var alert = new ResourceAlert
            {
                AlertId = alertId,
                ResourceType = resourceType,
                Severity = severity,
                Message = message,
                Timestamp = DateTime.UtcNow,
                CurrentValues = metrics
            };

            _activeAlerts.AddOrUpdate(alertId, alert, (_, __) => alert);
            _logger.LogWarning("Resource Alert: {AlertId} - {Message}", alertId, message);
        }

        private void ClearAlert(string alertId)
        {
            if (_activeAlerts.TryRemove(alertId, out var alert))
            {
                _logger.LogInformation("Resource Alert Cleared: {AlertId}", alertId);
            }
        }

        private string CalculateHealthStatus()
        {
            if (_activeAlerts.Values.Any(a => a.Severity == "Critical"))
                return "Critical";
            if (_activeAlerts.Values.Any(a => a.Severity == "Warning"))
                return "Warning";
            return "Healthy";
        }

        private Dictionary<string, double> CalculateAverageUtilization(List<ResourceMetricsModel> metrics)
        {
            if (!metrics.Any()) return new Dictionary<string, double>();

            return new Dictionary<string, double>
            {
                ["MemoryMB"] = metrics.Average(m => m.MemoryUsageBytes) / (1024.0 * 1024.0),
                ["CpuPercent"] = metrics.Average(m => m.CpuUsagePercent),
                ["Handles"] = metrics.Average(m => m.ActiveHandles)
            };
        }

        private Dictionary<string, double> CalculatePeakUtilization(List<ResourceMetricsModel> metrics)
        {
            if (!metrics.Any()) return new Dictionary<string, double>();

            return new Dictionary<string, double>
            {
                ["MemoryMB"] = metrics.Max(m => m.MemoryUsageBytes) / (1024.0 * 1024.0),
                ["CpuPercent"] = metrics.Max(m => m.CpuUsagePercent),
                ["Handles"] = metrics.Max(m => m.ActiveHandles)
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                await _monitoringTimer.DisposeAsync();
                _metricsHistory.Clear();
                _activeAlerts.Clear();
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}