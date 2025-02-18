using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class CachePerformanceMetrics
    {
        public DateTime TimestampUtc { get; set; }
        public double HitRatio { get; set; }
        public long AvgLookupLatencyMicroseconds { get; set; }
        public long MemoryUsageBytes { get; set; }
        public int EvictionCount { get; set; }
        public int NewEntriesCount { get; set; }
        public Dictionary<string, int> AccessPatternFrequency { get; set; }
        public double CacheWarmingEffectiveness { get; set; }
        public int InvalidationImpactedEntries { get; set; }
        public double MemoryUtilizationPercentage { get; set; }
        public long AverageCachedItemSize { get; set; }
        public int ConcurrentAccessCount { get; set; }
        public List<KeyValuePair<string, int>> TopAccessedKeys { get; set; }
        public int PartitionCount { get; set; }
        public Dictionary<string, double> PartitionHitRatios { get; set; }
        
        // Advanced eviction strategy metrics
        public string ActiveEvictionStrategy { get; set; }
        public Dictionary<string, double> EvictionStrategyMetrics { get; set; }
        public double AdaptiveCachingEffectiveness { get; set; }
        public int PredictivePreloadCount { get; set; }
        public double PreloadHitRatio { get; set; }
        public double MemoryPressurePercentage { get; set; }
        public Dictionary<string, int> EvictionsByCategory { get; set; }
        public Dictionary<string, double> CategoryHitRatios { get; set; }
        public double WeightedEvictionScore { get; set; }

        public CachePerformanceMetrics()
        {
            TimestampUtc = DateTime.UtcNow;
            AccessPatternFrequency = new Dictionary<string, int>();
            TopAccessedKeys = new List<KeyValuePair<string, int>>();
            PartitionHitRatios = new Dictionary<string, double>();
            EvictionStrategyMetrics = new Dictionary<string, double>();
            EvictionsByCategory = new Dictionary<string, int>();
            CategoryHitRatios = new Dictionary<string, double>();
        }
    }
}