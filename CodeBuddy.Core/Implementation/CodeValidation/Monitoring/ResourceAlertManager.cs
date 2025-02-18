using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IResourceAlertManager
    {
        Task ProcessMetricsAsync(Dictionary<ResourceMetricType, double> metrics, string validationContext);
        Task<IEnumerable<ResourceAlert>> GetActiveAlertsAsync();
        Task<IEnumerable<ResourceAlert>> GetHistoricalAlertsAsync(DateTime start, DateTime end);
        void Subscribe(Func<ResourceAlert, Task> handler);
        Task ConfigureAsync(AlertConfiguration configuration);
    }

    public class ResourceAlertManager : IResourceAlertManager
    {
        private readonly ILogger<ResourceAlertManager> _logger;
        private readonly ConcurrentDictionary<Guid, ResourceAlert> _activeAlerts;
        private readonly ConcurrentDictionary<DateTime, List<ResourceAlert>> _historicalAlerts;
        private readonly List<Func<ResourceAlert, Task>> _alertHandlers;
        private AlertConfiguration _configuration;
        private readonly ConcurrentDictionary<ResourceMetricType, Queue<(DateTime timestamp, double value)>> _metricHistory;
        private readonly object _alertLock = new object();

        public ResourceAlertManager(ILogger<ResourceAlertManager> logger)
        {
            _logger = logger;
            _activeAlerts = new ConcurrentDictionary<Guid, ResourceAlert>();
            _historicalAlerts = new ConcurrentDictionary<DateTime, List<ResourceAlert>>();
            _alertHandlers = new List<Func<ResourceAlert, Task>>();
            _metricHistory = new ConcurrentDictionary<ResourceMetricType, Queue<(DateTime, double)>>();
            _configuration = new AlertConfiguration();

            // Initialize metric history queues
            foreach (ResourceMetricType metric in Enum.GetValues(typeof(ResourceMetricType)))
            {
                _metricHistory[metric] = new Queue<(DateTime, double)>();
            }
        }

        public async Task ProcessMetricsAsync(Dictionary<ResourceMetricType, double> metrics, string validationContext)
        {
            foreach (var metric in metrics)
            {
                await ProcessSingleMetricAsync(metric.Key, metric.Value, validationContext);
                UpdateMetricHistory(metric.Key, metric.Value);
                if (_configuration.EnableTrendAnalysis)
                {
                    await AnalyzeTrendsAsync(metric.Key, validationContext);
                }
            }
        }

        private async Task ProcessSingleMetricAsync(ResourceMetricType metricType, double value, string validationContext)
        {
            if (!_configuration.Thresholds.TryGetValue(metricType, out var threshold))
                return;

            AlertSeverity? severity = DetermineAlertSeverity(value, threshold);
            if (severity.HasValue)
            {
                var alert = new ResourceAlert
                {
                    MetricType = metricType,
                    Severity = severity.Value,
                    CurrentValue = value,
                    ThresholdValue = GetThresholdValue(severity.Value, threshold),
                    ValidationContext = validationContext,
                    Message = GenerateAlertMessage(metricType, value, severity.Value)
                };

                if (ShouldGenerateAlert(alert))
                {
                    await RaiseAlertAsync(alert);
                }
            }
        }

        private bool ShouldGenerateAlert(ResourceAlert alert)
        {
            lock (_alertLock)
            {
                var recentAlerts = _activeAlerts.Values
                    .Where(a => a.MetricType == alert.MetricType &&
                               a.Timestamp >= DateTime.UtcNow.Subtract(_configuration.AlertAggregationWindow))
                    .ToList();

                return recentAlerts.Count < _configuration.MaxAlertsPerWindow;
            }
        }

        private async Task AnalyzeTrendsAsync(ResourceMetricType metricType, string validationContext)
        {
            var history = _metricHistory[metricType]
                .Where(h => h.timestamp >= DateTime.UtcNow.Subtract(_configuration.TrendAnalysisWindow))
                .ToList();

            if (history.Count < 2)
                return;

            // Analyze for sustained high usage
            var sustainedHighUsage = AnalyzeSustainedUsage(history, _configuration.Thresholds[metricType].WarningThreshold);
            if (sustainedHighUsage)
            {
                await RaiseAlertAsync(CreateTrendAlert(metricType, "Sustained high resource usage detected", AlertSeverity.Warning, validationContext));
            }

            // Analyze for rapid increases
            var rapidIncrease = AnalyzeRateOfChange(history, _configuration.Thresholds[metricType].RateOfChangeThreshold);
            if (rapidIncrease)
            {
                await RaiseAlertAsync(CreateTrendAlert(metricType, "Rapid resource usage increase detected", AlertSeverity.Critical, validationContext));
            }

            // Analyze for resource leaks
            var potentialLeak = AnalyzeResourceLeak(history);
            if (potentialLeak)
            {
                await RaiseAlertAsync(CreateTrendAlert(metricType, "Potential resource leak detected", AlertSeverity.Emergency, validationContext));
            }
        }

        private void UpdateMetricHistory(ResourceMetricType metricType, double value)
        {
            var history = _metricHistory[metricType];
            history.Enqueue((DateTime.UtcNow, value));

            // Trim old entries
            while (history.Count > 0 && 
                   history.Peek().timestamp < DateTime.UtcNow.Subtract(_configuration.TrendAnalysisWindow))
            {
                history.Dequeue();
            }
        }

        private AlertSeverity? DetermineAlertSeverity(double value, ResourceThreshold threshold)
        {
            if (value >= threshold.EmergencyThreshold)
                return AlertSeverity.Emergency;
            if (value >= threshold.CriticalThreshold)
                return AlertSeverity.Critical;
            if (value >= threshold.WarningThreshold)
                return AlertSeverity.Warning;
            return null;
        }

        private double GetThresholdValue(AlertSeverity severity, ResourceThreshold threshold)
        {
            return severity switch
            {
                AlertSeverity.Emergency => threshold.EmergencyThreshold,
                AlertSeverity.Critical => threshold.CriticalThreshold,
                AlertSeverity.Warning => threshold.WarningThreshold,
                _ => throw new ArgumentException("Invalid severity level")
            };
        }

        private string GenerateAlertMessage(ResourceMetricType metricType, double value, AlertSeverity severity)
        {
            return $"{severity} Alert: {metricType} usage at {value:F2}% exceeds threshold";
        }

        private async Task RaiseAlertAsync(ResourceAlert alert)
        {
            _activeAlerts.TryAdd(alert.Id, alert);
            
            var dateKey = alert.Timestamp.Date;
            _historicalAlerts.AddOrUpdate(
                dateKey,
                new List<ResourceAlert> { alert },
                (_, existing) =>
                {
                    existing.Add(alert);
                    return existing;
                });

            foreach (var handler in _alertHandlers)
            {
                try
                {
                    await handler(alert);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in alert handler");
                }
            }

            _logger.LogWarning("Resource Alert: {Message}", alert.Message);
        }

        private ResourceAlert CreateTrendAlert(ResourceMetricType metricType, string message, AlertSeverity severity, string validationContext)
        {
            return new ResourceAlert
            {
                MetricType = metricType,
                Message = message,
                Severity = severity,
                ValidationContext = validationContext
            };
        }

        private bool AnalyzeSustainedUsage(List<(DateTime timestamp, double value)> history, double threshold)
        {
            return history.Count > 0 && 
                   history.All(h => h.value >= threshold);
        }

        private bool AnalyzeRateOfChange(List<(DateTime timestamp, double value)> history, double threshold)
        {
            if (history.Count < 2)
                return false;

            var first = history.First();
            var last = history.Last();
            var timeSpan = (last.timestamp - first.timestamp).TotalMinutes;
            var rateOfChange = (last.value - first.value) / timeSpan;

            return rateOfChange > threshold;
        }

        private bool AnalyzeResourceLeak(List<(DateTime timestamp, double value)> history)
        {
            if (history.Count < 3)
                return false;

            // Simple linear regression to detect consistent upward trend
            var n = history.Count;
            var timestamps = history.Select(h => h.timestamp.Ticks).ToList();
            var values = history.Select(h => h.value).ToList();

            var meanX = timestamps.Average();
            var meanY = values.Average();

            var slope = timestamps.Zip(values, (x, y) => (x - meanX) * (y - meanY)).Sum() /
                       timestamps.Sum(x => Math.Pow(x - meanX, 2));

            // Positive slope indicates increasing trend
            return slope > 0;
        }

        public Task<IEnumerable<ResourceAlert>> GetActiveAlertsAsync()
        {
            return Task.FromResult(_activeAlerts.Values.AsEnumerable());
        }

        public Task<IEnumerable<ResourceAlert>> GetHistoricalAlertsAsync(DateTime start, DateTime end)
        {
            var alerts = _historicalAlerts
                .Where(kv => kv.Key >= start.Date && kv.Key <= end.Date)
                .SelectMany(kv => kv.Value)
                .Where(a => a.Timestamp >= start && a.Timestamp <= end);

            return Task.FromResult(alerts);
        }

        public void Subscribe(Func<ResourceAlert, Task> handler)
        {
            _alertHandlers.Add(handler);
        }

        public Task ConfigureAsync(AlertConfiguration configuration)
        {
            _configuration = configuration;
            return Task.CompletedTask;
        }
    }
}