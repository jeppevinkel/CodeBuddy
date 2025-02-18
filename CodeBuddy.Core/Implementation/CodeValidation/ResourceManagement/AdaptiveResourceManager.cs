using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.ResourceManagement;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement
{
    public class AdaptiveResourceManager
    {
        private readonly TimeSeriesStorage _timeSeriesStorage;
        private readonly ResourceTrendAnalyzer _resourceTrendAnalyzer;
        private readonly ResourceMonitoringDashboard _monitoringDashboard;
        private readonly MemoryLeakPreventionSystem _memoryLeakPrevention;
        private readonly AdaptiveResourceConfig _config;
        private readonly ConcurrentDictionary<string, Queue<ResourceUsagePattern>> _usagePatterns;
        private readonly ConcurrentDictionary<string, ResourcePrediction> _predictions;
        private readonly IResourceLoggingService _loggingService;
        
        public AdaptiveResourceManager(
            TimeSeriesStorage timeSeriesStorage,
            ResourceTrendAnalyzer resourceTrendAnalyzer,
            ResourceMonitoringDashboard monitoringDashboard,
            MemoryLeakPreventionSystem memoryLeakPrevention,
            AdaptiveResourceConfig config,
            IResourceLoggingService loggingService)
        {
            _timeSeriesStorage = timeSeriesStorage;
            _resourceTrendAnalyzer = resourceTrendAnalyzer;
            _monitoringDashboard = monitoringDashboard;
            _memoryLeakPrevention = memoryLeakPrevention;
            _config = config;
            _usagePatterns = new ConcurrentDictionary<string, Queue<ResourceUsagePattern>>();
            _predictions = new ConcurrentDictionary<string, ResourcePrediction>();
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public async Task CollectResourceMetrics(string validationType, ResourceUsagePattern pattern)
        {
            var metrics = new Dictionary<string, object>
            {
                { "ValidationType", validationType },
                { "MemoryUsage", pattern.MemoryUsage },
                { "ProcessingDuration", pattern.ProcessingDuration },
                { "PatternCollectionTime", DateTime.UtcNow }
            };
            
            _loggingService.LogResourceAllocation(
                validationType,
                "MetricsCollection",
                metrics,
                nameof(AdaptiveResourceManager));

            var patterns = _usagePatterns.GetOrAdd(validationType, _ => new Queue<ResourceUsagePattern>());
            patterns.Enqueue(pattern);

            // Keep only recent patterns for analysis
            while (patterns.Count > 100)
            {
                patterns.TryDequeue(out _);
            }

            await _timeSeriesStorage.StoreMetricsAsync(validationType, pattern);
            await UpdatePredictions(validationType);

            metrics["PredictionUpdated"] = true;
            metrics["PredictionConfidence"] = _predictions.TryGetValue(validationType, out var pred) ? pred.PredictionConfidence : 0;
            
            _loggingService.LogResourceAllocation(
                validationType,
                "PredictionUpdate",
                metrics,
                nameof(AdaptiveResourceManager));
        }

        public async Task<ResourceAllocation> GetOptimalResourceAllocation(string validationType)
        {
            var metrics = new Dictionary<string, object>
            {
                { "ValidationType", validationType },
                { "RequestTime", DateTime.UtcNow }
            };

            if (_predictions.TryGetValue(validationType, out var prediction) && 
                prediction.PredictionConfidence >= _config.PredictionThreshold)
            {
                var allocation = new ResourceAllocation
                {
                    ThreadCount = prediction.RecommendedThreadCount,
                    MemoryLimit = prediction.ExpectedMemoryUsage,
                    QueueSize = CalculateOptimalQueueSize(prediction),
                    BatchSize = CalculateOptimalBatchSize(prediction)
                };

                metrics["AllocationType"] = "Predicted";
                metrics["ThreadCount"] = allocation.ThreadCount;
                metrics["MemoryLimit"] = allocation.MemoryLimit;
                metrics["QueueSize"] = allocation.QueueSize;
                metrics["BatchSize"] = allocation.BatchSize;
                metrics["PredictionConfidence"] = prediction.PredictionConfidence;

                _loggingService.LogResourceAllocation(
                    validationType,
                    "OptimalAllocation",
                    metrics,
                    nameof(AdaptiveResourceManager));

                return allocation;
            }

            // Fallback to default allocation if prediction not available
            var defaultAllocation = new ResourceAllocation
                {
                    ThreadCount = prediction.RecommendedThreadCount,
                    MemoryLimit = prediction.ExpectedMemoryUsage,
                    QueueSize = CalculateOptimalQueueSize(prediction),
                    BatchSize = CalculateOptimalBatchSize(prediction)
                };
            }

            metrics["AllocationType"] = "Default";
            metrics["ThreadCount"] = defaultAllocation.ThreadCount;
            metrics["MemoryLimit"] = defaultAllocation.MemoryLimit;
            metrics["QueueSize"] = defaultAllocation.QueueSize;
            metrics["BatchSize"] = defaultAllocation.BatchSize;
            
            _loggingService.LogResourceWarning(
                validationType,
                "Using default allocation due to insufficient prediction confidence",
                metrics,
                nameof(AdaptiveResourceManager));

            return defaultAllocation;
            {
                ThreadCount = _config.MinThreadPoolSize,
                MemoryLimit = _config.MinMemoryThreshold,
                QueueSize = _config.MinQueueSize,
                BatchSize = _config.BatchSize
            };
        }

        private async Task UpdatePredictions(string validationType)
        {
            if (!_usagePatterns.TryGetValue(validationType, out var patterns) || patterns.Count < 10)
            {
                _loggingService.LogResourceWarning(
                    validationType,
                    "Insufficient patterns for prediction update",
                    new Dictionary<string, object> {
                        { "PatternCount", patterns?.Count ?? 0 },
                        { "MinimumRequired", 10 }
                    },
                    nameof(AdaptiveResourceManager));
                return;
            }

            var trends = await _resourceTrendAnalyzer.AnalyzeTrends(patterns.ToList());
            var prediction = new ResourcePrediction
            {
                ValidationType = validationType,
                ExpectedMemoryUsage = CalculateExpectedMemoryUsage(trends),
                RecommendedThreadCount = CalculateOptimalThreadCount(trends),
                EstimatedDuration = CalculateEstimatedDuration(trends),
                PredictionConfidence = CalculatePredictionConfidence(trends)
            };

            _predictions.AddOrUpdate(validationType, prediction, (_, __) => prediction);
            await UpdateDashboard(validationType, prediction);

            var metrics = new Dictionary<string, object>
            {
                { "ValidationType", validationType },
                { "PredictionConfidence", prediction.PredictionConfidence },
                { "ExpectedMemoryUsage", prediction.ExpectedMemoryUsage },
                { "RecommendedThreadCount", prediction.RecommendedThreadCount },
                { "EstimatedDuration", prediction.EstimatedDuration }
            };

            _loggingService.LogResourceAllocation(
                validationType,
                "PredictionCreated",
                metrics,
                nameof(AdaptiveResourceManager));
        }

        private async Task UpdateDashboard(string validationType, ResourcePrediction prediction)
        {
            var metrics = new ResourceOptimizationMetrics
            {
                PredictionAccuracy = CalculatePredictionAccuracy(validationType),
                ResourceUtilizationPercentage = CalculateResourceUtilization(),
                EmergencyCleanupCount = await _memoryLeakPrevention.GetEmergencyCleanupCount(),
                AverageResponseTime = CalculateAverageResponseTime(validationType),
                ResourceExhaustionCount = await _memoryLeakPrevention.GetResourceExhaustionCount(),
                ValidationTypePerformance = GetValidationTypePerformance(),
                Recommendations = GenerateRecommendations(prediction)
            };

            await _monitoringDashboard.UpdateMetrics(metrics);
        }

        private int CalculateOptimalQueueSize(ResourcePrediction prediction)
        {
            var baseSize = (int)(prediction.EstimatedDuration.TotalMilliseconds / 100);
            return Math.Max(_config.MinQueueSize, 
                   Math.Min(baseSize, _config.MaxQueueSize));
        }

        private int CalculateOptimalBatchSize(ResourcePrediction prediction)
        {
            var optimalSize = (int)(prediction.ExpectedMemoryUsage / (1024 * 1024) / 10);
            return Math.Max(1, Math.Min(optimalSize, _config.BatchSize));
        }

        private long CalculateExpectedMemoryUsage(Dictionary<string, double> trends)
        {
            var baseMemory = trends.GetValueOrDefault("memory_trend", 100);
            return (long)(baseMemory * 1.2); // Add 20% buffer
        }

        private int CalculateOptimalThreadCount(Dictionary<string, double> trends)
        {
            var baseThreads = trends.GetValueOrDefault("thread_efficiency", 1.0);
            return Math.Max(_config.MinThreadPoolSize,
                   Math.Min((int)(baseThreads * 2), _config.MaxThreadPoolSize));
        }

        private TimeSpan CalculateEstimatedDuration(Dictionary<string, double> trends)
        {
            var baseDuration = trends.GetValueOrDefault("duration_trend", 1000);
            return TimeSpan.FromMilliseconds(baseDuration);
        }

        private double CalculatePredictionConfidence(Dictionary<string, double> trends)
        {
            return trends.GetValueOrDefault("prediction_confidence", 0.5);
        }

        private double CalculatePredictionAccuracy(string validationType)
        {
            if (!_usagePatterns.TryGetValue(validationType, out var patterns) || 
                !_predictions.TryGetValue(validationType, out var prediction))
            {
                return 0;
            }

            var actualUsage = patterns.Average(p => p.MemoryUsage);
            var predictedUsage = prediction.ExpectedMemoryUsage;
            return 1 - Math.Abs(actualUsage - predictedUsage) / (double)actualUsage;
        }

        private int CalculateResourceUtilization()
        {
            var totalMemory = GC.GetTotalMemory(false);
            return (int)(totalMemory * 100 / _config.MaxMemoryThreshold);
        }

        private double CalculateAverageResponseTime(string validationType)
        {
            if (!_usagePatterns.TryGetValue(validationType, out var patterns))
            {
                return 0;
            }

            return patterns.Average(p => p.ProcessingDuration.TotalMilliseconds);
        }

        private Dictionary<string, double> GetValidationTypePerformance()
        {
            return _usagePatterns.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average(p => p.ProcessingDuration.TotalMilliseconds));
        }

        private List<string> GenerateRecommendations(ResourcePrediction prediction)
        {
            var recommendations = new List<string>();

            if (prediction.ExpectedMemoryUsage > _config.MaxMemoryThreshold * 0.8)
            {
                recommendations.Add("Consider increasing maximum memory threshold");
            }

            if (prediction.RecommendedThreadCount >= _config.MaxThreadPoolSize * 0.9)
            {
                recommendations.Add("Consider increasing maximum thread pool size");
            }

            if (prediction.PredictionConfidence < 0.6)
            {
                recommendations.Add("Collect more validation data to improve prediction accuracy");
            }

            return recommendations;
        }
    }

    public class ResourceAllocation
    {
        public int ThreadCount { get; set; }
        public long MemoryLimit { get; set; }
        public int QueueSize { get; set; }
        public int BatchSize { get; set; }
    }
}