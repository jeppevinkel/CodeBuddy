using System;

namespace CodeBuddy.Core.Models;

public class ValidationResilienceConfig
{
    // Distributed Monitoring Configuration
    public bool EnableDistributedMonitoring { get; set; } = true;
    public TimeSpan NodeSyncInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MinHealthyNodes { get; set; } = 2;
    public double ClusterWideCpuThreshold { get; set; } = 75.0;
    public double ClusterWideMemoryThreshold { get; set; } = 80.0;
    public int MaxNodesInCluster { get; set; } = 10;
    public TimeSpan NodeHealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int NodeFailureThreshold { get; set; } = 3;
    public TimeSpan FailoverGracePeriod { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnableAutomaticFailover { get; set; } = true;
    public LoadBalancingStrategy LoadBalancingStrategy { get; set; } = LoadBalancingStrategy.ResourceAware;
    
    // Predictive Scaling Configuration
    public bool PredictiveScalingEnabled { get; set; } = true;
    public TimeSpan HistoricalDataRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public double PredictionConfidenceThreshold { get; set; } = 0.8; // 80% confidence required for scaling decisions
    public TimeSpan ScalingLeadTime { get; set; } = TimeSpan.FromMinutes(5); // How far ahead to predict
    public int MinDataPointsForPrediction { get; set; } = 100; // Minimum historical data points needed
    public TimeSpan PredictionInterval { get; set; } = TimeSpan.FromSeconds(30); // How often to update predictions
    public double ScalingGradualityFactor { get; set; } = 0.2; // Max 20% change in resources per adjustment
    public bool EnablePredictionMetrics { get; set; } = true;
    
    // Resource Throttling Configuration
    public double MaxCpuThresholdPercent { get; set; } = 80.0;
    public double MaxMemoryThresholdMB { get; set; } = 1024.0; // 1GB
    public double MaxDiskIoMBPS { get; set; } = 100.0; // 100 MB/s
    public int MaxConcurrentValidations { get; set; } = 10;

    // Memory Analytics Configuration
    public TimeSpan MemoryAnalysisInterval { get; set; } = TimeSpan.FromMinutes(10);
    public double MemoryGrowthThresholdPercent { get; set; } = 5.0; // Threshold for leak detection
    public int LeakConfidenceThreshold { get; set; } = 90; // Confidence level (percentage)
    public bool EnableAutomaticMemoryDump { get; set; } = true;
    public int MemorySamplingRate { get; set; } = 60; // Samples per hour
    public double LohGrowthThresholdMB { get; set; } = 100.0; // Large Object Heap growth threshold
    public int MaxFinalizationQueueLength { get; set; } = 1000;
    public double MaxFragmentationPercent { get; set; } = 40.0;
    
    // Memory Analytics Dashboard Configuration
    public bool EnableMemoryAnalyticsDashboard { get; set; } = true;
    public TimeSpan MemoryMetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public int MemoryMetricsSamplingInterval { get; set; } = 60; // Seconds
    public bool EnableRealTimeMemoryMonitoring { get; set; } = true;
    public int MaxMemoryGraphDataPoints { get; set; } = 1000;
    public double AllocationHotspotThresholdMB { get; set; } = 50.0;
    public bool EnableMemoryLeakAlerts { get; set; } = true;
    
    // Adaptive Throttling Configuration
    public TimeSpan ResourceTrendInterval { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableAdaptiveThrottling { get; set; } = true;
    public double ThrottlingAdjustmentFactor { get; set; } = 0.1; // 10% adjustment
    public int ResourceReservationPercent { get; set; } = 20; // Reserve 20% for critical validations
    
    // Existing Resilience Configuration
    public int MaxRetryAttempts { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerResetTime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MiddlewareTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableFallbackBehavior { get; set; } = true;
    public bool ContinueOnMiddlewareFailure { get; set; } = true;
    public LogLevel FailureLogLevel { get; set; } = LogLevel.Error;
    public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.ExponentialBackoff;
    public Dictionary<string, MiddlewareResilienceConfig> MiddlewareSpecificConfig { get; set; } = new();
}

public class MiddlewareResilienceConfig
{
    public int? MaxRetryAttempts { get; set; }
    public int? CircuitBreakerThreshold { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool? EnableFallback { get; set; }
    public RetryStrategy? RetryStrategy { get; set; }
    public string FallbackAction { get; set; }
}

public enum RetryStrategy
{
    Immediate,
    LinearBackoff,
    ExponentialBackoff,
    Custom
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public enum LoadBalancingStrategy
{
    RoundRobin,
    ResourceAware,
    LeastConnections,
    WeightedResponse,
    Predictive
}