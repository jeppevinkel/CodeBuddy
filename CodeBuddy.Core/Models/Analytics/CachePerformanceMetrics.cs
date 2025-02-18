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

        public CachePerformanceMetrics()
        {
            TimestampUtc = DateTime.UtcNow;
            AccessPatternFrequency = new Dictionary<string, int>();
            TopAccessedKeys = new List<KeyValuePair<string, int>>();
            PartitionHitRatios = new Dictionary<string, double>();
        }
    }
}