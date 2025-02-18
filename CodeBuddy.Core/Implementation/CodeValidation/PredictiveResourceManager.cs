using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class PredictiveResourceManager
{
    private readonly ILogger<PredictiveResourceManager> _logger;
    private readonly ValidationResilienceConfig _config;
    private readonly IResourceAnalytics _resourceAnalytics;
    private readonly IMetricsAggregator _metricsAggregator;
    private readonly ConcurrentQueue<ResourcePrediction> _predictions;
    private readonly ConcurrentDictionary<string, List<ResourceUsageData>> _historicalData;
    private readonly object _predictionLock = new object();
    private bool _isInitialized;
    private DateTime _lastPredictionUpdate;

    public PredictiveResourceManager(
        ILogger<PredictiveResourceManager> logger,
        ValidationResilienceConfig config,
        IResourceAnalytics resourceAnalytics,
        IMetricsAggregator metricsAggregator)
    {
        _logger = logger;
        _config = config;
        _resourceAnalytics = resourceAnalytics;
        _metricsAggregator = metricsAggregator;
        _predictions = new ConcurrentQueue<ResourcePrediction>();
        _historicalData = new ConcurrentDictionary<string, List<ResourceUsageData>>();
        _lastPredictionUpdate = DateTime.MinValue;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        lock (_predictionLock)
        {
            if (_isInitialized) return;
            _isInitialized = true;
        }

        // Load historical data
        var retentionStart = DateTime.UtcNow.Subtract(_config.HistoricalDataRetentionPeriod);
        var historicalData = await _resourceAnalytics.GetHistoricalResourceDataAsync(retentionStart);
        
        foreach (var data in historicalData.GroupBy(d => d.PipelineId))
        {
            _historicalData[data.Key] = data.OrderBy(d => d.Timestamp).ToList();
        }

        // Start prediction update task
        _ = Task.Run(PredictionUpdateLoopAsync);
    }

    public async Task<ResourceScalingRecommendation> GetScalingRecommendationAsync(string pipelineId)
    {
        if (!_config.PredictiveScalingEnabled)
        {
            return new ResourceScalingRecommendation 
            { 
                ShouldScale = false,
                Confidence = 0,
                RecommendedConcurrency = null
            };
        }

        var currentPredictions = _predictions
            .Where(p => p.PipelineId == pipelineId)
            .OrderBy(p => p.PredictedTimestamp)
            .ToList();

        if (!currentPredictions.Any() || 
            currentPredictions.Max(p => p.Confidence) < _config.PredictionConfidenceThreshold)
        {
            return new ResourceScalingRecommendation 
            { 
                ShouldScale = false,
                Confidence = currentPredictions.Any() ? currentPredictions.Max(p => p.Confidence) : 0,
                RecommendedConcurrency = null
            };
        }

        var nearestPrediction = currentPredictions
            .OrderBy(p => Math.Abs((p.PredictedTimestamp - DateTime.UtcNow).TotalSeconds))
            .First();

        var currentUsage = await _resourceAnalytics.GetCurrentResourceUsageAsync(pipelineId);
        var recommendedConcurrency = CalculateRecommendedConcurrency(nearestPrediction, currentUsage);

        return new ResourceScalingRecommendation
        {
            ShouldScale = true,
            Confidence = nearestPrediction.Confidence,
            RecommendedConcurrency = recommendedConcurrency,
            PredictedCpuUsage = nearestPrediction.PredictedCpuUsage,
            PredictedMemoryUsage = nearestPrediction.PredictedMemoryUsage,
            PredictedTimestamp = nearestPrediction.PredictedTimestamp
        };
    }

    private async Task PredictionUpdateLoopAsync()
    {
        while (true)
        {
            try
            {
                if (_config.PredictiveScalingEnabled && 
                    DateTime.UtcNow - _lastPredictionUpdate >= _config.PredictionInterval)
                {
                    await UpdatePredictionsAsync();
                    _lastPredictionUpdate = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating resource predictions");
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private async Task UpdatePredictionsAsync()
    {
        foreach (var pipelineData in _historicalData)
        {
            var pipelineId = pipelineData.Key;
            var dataPoints = pipelineData.Value;

            if (dataPoints.Count < _config.MinDataPointsForPrediction)
            {
                _logger.LogWarning(
                    "Insufficient data points for prediction. Pipeline: {PipelineId}, Points: {Points}, Required: {Required}",
                    pipelineId, dataPoints.Count, _config.MinDataPointsForPrediction);
                continue;
            }

            var predictions = GenerateResourcePredictions(pipelineId, dataPoints);
            
            // Clear old predictions for this pipeline
            while (_predictions.TryDequeue(out var _)) { }

            // Add new predictions
            foreach (var prediction in predictions)
            {
                _predictions.Enqueue(prediction);
            }

            // Record prediction metrics if enabled
            if (_config.EnablePredictionMetrics)
            {
                var confidence = predictions.Average(p => p.Confidence);
                _metricsAggregator.RecordPredictionAccuracy(pipelineId, confidence);
            }
        }
    }

    private List<ResourcePrediction> GenerateResourcePredictions(string pipelineId, List<ResourceUsageData> historicalData)
    {
        var predictions = new List<ResourcePrediction>();
        var now = DateTime.UtcNow;

        // Group data by hour of day and day of week to identify patterns
        var patterns = historicalData
            .GroupBy(d => new { d.Timestamp.DayOfWeek, Hour = d.Timestamp.Hour })
            .Select(g => new
            {
                g.Key.DayOfWeek,
                g.Key.Hour,
                AvgCpu = g.Average(d => d.CpuUsagePercentage),
                AvgMemory = g.Average(d => d.MemoryUsageMB),
                Confidence = Math.Min(1.0, g.Count() / 100.0) // Confidence based on sample size
            })
            .ToList();

        // Generate predictions for the next interval
        for (var i = 0; i < _config.ScalingLeadTime.TotalMinutes; i += 5)
        {
            var predictionTime = now.AddMinutes(i);
            var pattern = patterns.FirstOrDefault(p => 
                p.DayOfWeek == predictionTime.DayOfWeek && 
                p.Hour == predictionTime.Hour);

            if (pattern == null) continue;

            predictions.Add(new ResourcePrediction
            {
                PipelineId = pipelineId,
                PredictedTimestamp = predictionTime,
                PredictedCpuUsage = pattern.AvgCpu,
                PredictedMemoryUsage = pattern.AvgMemory,
                Confidence = pattern.Confidence
            });
        }

        return predictions;
    }

    private int CalculateRecommendedConcurrency(ResourcePrediction prediction, ResourceUsageData currentUsage)
    {
        // Calculate base recommendation using CPU as primary metric
        var cpuBasedConcurrency = (int)(_config.MaxConcurrentValidations * 
            (1 - (prediction.PredictedCpuUsage / 100.0)));

        // Adjust for memory constraints
        var memoryBasedConcurrency = (int)(_config.MaxConcurrentValidations *
            (1 - (prediction.PredictedMemoryUsage / _config.MaxMemoryThresholdMB)));

        // Take the more conservative value
        var recommendedConcurrency = Math.Min(cpuBasedConcurrency, memoryBasedConcurrency);

        // Apply gradual scaling to prevent large swings
        var currentConcurrency = (int)(_config.MaxConcurrentValidations * 
            (1 - (currentUsage.CpuUsagePercentage / 100.0)));
        var maxChange = (int)(_config.MaxConcurrentValidations * _config.ScalingGradualityFactor);
        
        return Math.Min(
            Math.Max(
                currentConcurrency - maxChange,
                recommendedConcurrency
            ),
            currentConcurrency + maxChange
        );
    }
}

public class ResourcePrediction
{
    public string PipelineId { get; set; }
    public DateTime PredictedTimestamp { get; set; }
    public double PredictedCpuUsage { get; set; }
    public double PredictedMemoryUsage { get; set; }
    public double Confidence { get; set; }
}

public class ResourceScalingRecommendation
{
    public bool ShouldScale { get; set; }
    public double Confidence { get; set; }
    public int? RecommendedConcurrency { get; set; }
    public double? PredictedCpuUsage { get; set; }
    public double? PredictedMemoryUsage { get; set; }
    public DateTime? PredictedTimestamp { get; set; }
}