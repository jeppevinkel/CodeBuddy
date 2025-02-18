using System;

namespace CodeBuddy.Core.Models
{
    public enum CacheEvictionStrategy
    {
        LRU,
        LFU,
        FIFO,
        WeightedCombination
    }

    public class ValidationCacheConfig
    {
        public int MaxEntries { get; set; } = 10000;
        public CacheEvictionStrategy EvictionStrategy { get; set; } = CacheEvictionStrategy.WeightedCombination;
        public int MaxCacheSizeMB { get; set; } = 256;
        public int DefaultTTLMinutes { get; set; } = 60;
        public bool EnableCacheWarming { get; set; } = true;
        
        // Adaptive caching settings
        public bool EnableAdaptiveCaching { get; set; } = true;
        public double AdaptiveHitRatioThreshold { get; set; } = 0.7;
        public int AdaptiveAdjustmentIntervalMinutes { get; set; } = 5;
        public double MemoryPressureThresholdPercent { get; set; } = 80.0;
        public bool EnablePredictivePreloading { get; set; } = true;
        public int MaxPreloadedEntries { get; set; } = 1000;
        
        // Performance monitoring thresholds
        public double MinAcceptableHitRatio { get; set; } = 0.5;
        public long MaxAcceptableLatencyMicroseconds { get; set; } = 1000;
        public int MaxMemoryUtilizationPercent { get; set; } = 90;
        public int MetricsRetentionDays { get; set; } = 30;
        public int MonitoringIntervalSeconds { get; set; } = 60;
        public bool EnablePerformanceAlerts { get; set; } = true;
    }
}