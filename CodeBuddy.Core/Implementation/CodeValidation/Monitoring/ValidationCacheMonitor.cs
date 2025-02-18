using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using Microsoft.Extensions.Options;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class ValidationCacheMonitor : IValidationCacheMonitor
    {
        private readonly ValidationCacheConfig _config;
        private readonly ConcurrentDictionary<string, List<double>> _strategyMetricsHistory;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<OperationMetric>> _operationMetrics;
        private readonly ConcurrentDictionary<string, double> _strategyEffectiveness;
        private DateTime _lastCleanup = DateTime.UtcNow;

        public ValidationCacheMonitor(IOptions<ValidationCacheConfig> config)
        {
            _config = config.Value;
            _strategyMetricsHistory = new ConcurrentDictionary<string, List<double>>();
            _operationMetrics = new ConcurrentDictionary<string, ConcurrentQueue<OperationMetric>>();
            _strategyEffectiveness = new ConcurrentDictionary<string, double>();
        }

        public async Task RecordOperationMetricsAsync(string operation, long latencyMicroseconds, bool success)
        {
            var metrics = _operationMetrics.GetOrAdd(operation, _ => new ConcurrentQueue<OperationMetric>());
            
            metrics.Enqueue(new OperationMetric
            {
                Timestamp = DateTime.UtcNow,
                LatencyMicroseconds = latencyMicroseconds,
                Success = success
            });

            await CleanupOldMetricsAsync();
        }

        public async Task AlertIfThresholdExceededAsync(CachePerformanceMetrics metrics)
        {
            if (!_config.EnablePerformanceAlerts)
                return;

            var alerts = new List<string>();

            if (metrics.HitRatio < _config.MinAcceptableHitRatio)
                alerts.Add($"Cache hit ratio ({metrics.HitRatio:P2}) below threshold ({_config.MinAcceptableHitRatio:P2})");

            if (metrics.MemoryUtilizationPercentage > _config.MaxMemoryUtilizationPercent)
                alerts.Add($"Memory utilization ({metrics.MemoryUtilizationPercentage:F1}%) above threshold ({_config.MaxMemoryUtilizationPercent}%)");

            if (metrics.AvgLookupLatencyMicroseconds > _config.MaxAcceptableLatencyMicroseconds)
                alerts.Add($"Average lookup latency ({metrics.AvgLookupLatencyMicroseconds}μs) above threshold ({_config.MaxAcceptableLatencyMicroseconds}μs)");

            // In a real implementation, these alerts would be sent to a monitoring system
            foreach (var alert in alerts)
            {
                // Log or send alert
                Console.WriteLine($"[CACHE ALERT] {alert}");
            }
        }

        public async Task RecordStrategyMetricsAsync(string strategy, IDictionary<string, double> metrics)
        {
            var history = _strategyMetricsHistory.GetOrAdd(strategy, _ => new List<double>());
            
            // Calculate effectiveness score based on metrics
            double effectivenessScore = CalculateEffectivenessScore(metrics);
            
            history.Add(effectivenessScore);
            _strategyEffectiveness.AddOrUpdate(strategy, effectivenessScore, (_, __) => effectivenessScore);

            while (history.Count > 100) // Keep last 100 measurements
                history.RemoveAt(0);
        }

        public async Task<IDictionary<string, double>> GetStrategyEffectivenessAsync()
        {
            return new Dictionary<string, double>(_strategyEffectiveness);
        }

        public async Task UpdateAdaptiveCacheMetricsAsync(double hitRatio, double memoryPressure)
        {
            // Record adaptive caching metrics for analysis
            var metrics = _operationMetrics.GetOrAdd("AdaptiveCaching", _ => new ConcurrentQueue<OperationMetric>());
            
            metrics.Enqueue(new OperationMetric
            {
                Timestamp = DateTime.UtcNow,
                HitRatio = hitRatio,
                MemoryPressure = memoryPressure
            });
        }

        private double CalculateEffectivenessScore(IDictionary<string, double> metrics)
        {
            // This is a simplified scoring mechanism
            // In a real implementation, this would be more sophisticated
            double score = 0;
            int count = 0;

            foreach (var metric in metrics)
            {
                switch (metric.Key)
                {
                    case "avg_age_seconds":
                        score += NormalizeAge(metric.Value);
                        count++;
                        break;
                    case "avg_access_count":
                        score += NormalizeAccessCount(metric.Value);
                        count++;
                        break;
                    case "hit_ratio":
                        score += metric.Value;
                        count++;
                        break;
                }
            }

            return count > 0 ? score / count : 0;
        }

        private double NormalizeAge(double ageSeconds)
        {
            // Normalize age to a 0-1 scale
            // Assuming 24 hours is the maximum desirable age
            const double maxAge = 24 * 60 * 60;
            return Math.Max(0, 1 - (ageSeconds / maxAge));
        }

        private double NormalizeAccessCount(double count)
        {
            // Normalize access count to a 0-1 scale
            // Assuming 1000 accesses is the maximum we care about
            const double maxCount = 1000;
            return Math.Min(1, count / maxCount);
        }

        private async Task CleanupOldMetricsAsync()
        {
            if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5)
                return;

            _lastCleanup = DateTime.UtcNow;
            var cutoff = DateTime.UtcNow.AddDays(-_config.MetricsRetentionDays);

            foreach (var metrics in _operationMetrics.Values)
            {
                while (metrics.TryPeek(out var metric) && metric.Timestamp < cutoff)
                {
                    metrics.TryDequeue(out _);
                }
            }
        }

        private class OperationMetric
        {
            public DateTime Timestamp { get; set; }
            public long LatencyMicroseconds { get; set; }
            public bool Success { get; set; }
            public double HitRatio { get; set; }
            public double MemoryPressure { get; set; }
        }
    }
}