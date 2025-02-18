using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation.Caching
{
    public class LRUEvictionStrategy : ICacheEvictionStrategy
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastAccess = new();
        
        public string Name => "LRU";

        public Task RecordAccessAsync(string key, bool isHit)
        {
            _lastAccess.AddOrUpdate(key, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> SelectEntriesForEvictionAsync(int count, IDictionary<string, CacheEntryMetadata> entries)
        {
            var result = entries
                .Where(e => _lastAccess.ContainsKey(e.Key))
                .OrderBy(e => _lastAccess[e.Key])
                .Take(count)
                .Select(e => e.Key);
            
            return Task.FromResult(result);
        }

        public Task<IDictionary<string, double>> GetMetricsAsync()
        {
            var metrics = new Dictionary<string, double>
            {
                ["avg_age_seconds"] = _lastAccess.Any() 
                    ? _lastAccess.Average(x => (DateTime.UtcNow - x.Value).TotalSeconds)
                    : 0
            };
            return Task.FromResult((IDictionary<string, double>)metrics);
        }

        public Task ResetAsync()
        {
            _lastAccess.Clear();
            return Task.CompletedTask;
        }
    }

    public class LFUEvictionStrategy : ICacheEvictionStrategy
    {
        private readonly ConcurrentDictionary<string, int> _accessCount = new();
        
        public string Name => "LFU";

        public Task RecordAccessAsync(string key, bool isHit)
        {
            _accessCount.AddOrUpdate(key, 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> SelectEntriesForEvictionAsync(int count, IDictionary<string, CacheEntryMetadata> entries)
        {
            var result = entries
                .Where(e => _accessCount.ContainsKey(e.Key))
                .OrderBy(e => _accessCount[e.Key])
                .Take(count)
                .Select(e => e.Key);
            
            return Task.FromResult(result);
        }

        public Task<IDictionary<string, double>> GetMetricsAsync()
        {
            var metrics = new Dictionary<string, double>
            {
                ["avg_access_count"] = _accessCount.Any() 
                    ? _accessCount.Average(x => x.Value) 
                    : 0
            };
            return Task.FromResult((IDictionary<string, double>)metrics);
        }

        public Task ResetAsync()
        {
            _accessCount.Clear();
            return Task.CompletedTask;
        }
    }

    public class FIFOEvictionStrategy : ICacheEvictionStrategy
    {
        private readonly ConcurrentDictionary<string, DateTime> _creationTime = new();
        
        public string Name => "FIFO";

        public Task RecordAccessAsync(string key, bool isHit)
        {
            _creationTime.TryAdd(key, DateTime.UtcNow);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> SelectEntriesForEvictionAsync(int count, IDictionary<string, CacheEntryMetadata> entries)
        {
            var result = entries
                .Where(e => _creationTime.ContainsKey(e.Key))
                .OrderBy(e => _creationTime[e.Key])
                .Take(count)
                .Select(e => e.Key);
            
            return Task.FromResult(result);
        }

        public Task<IDictionary<string, double>> GetMetricsAsync()
        {
            var metrics = new Dictionary<string, double>
            {
                ["avg_lifetime_seconds"] = _creationTime.Any() 
                    ? _creationTime.Average(x => (DateTime.UtcNow - x.Value).TotalSeconds)
                    : 0
            };
            return Task.FromResult((IDictionary<string, double>)metrics);
        }

        public Task ResetAsync()
        {
            _creationTime.Clear();
            return Task.CompletedTask;
        }
    }

    public class WeightedCombinationStrategy : ICacheEvictionStrategy
    {
        private readonly LRUEvictionStrategy _lru = new();
        private readonly LFUEvictionStrategy _lfu = new();
        private readonly ConcurrentDictionary<string, CacheEntryMetadata> _metadata = new();
        
        public string Name => "WeightedCombination";

        public async Task RecordAccessAsync(string key, bool isHit)
        {
            await _lru.RecordAccessAsync(key, isHit);
            await _lfu.RecordAccessAsync(key, isHit);
            
            var metadata = _metadata.GetOrAdd(key, _ => new CacheEntryMetadata());
            metadata.LastAccessedAt = DateTime.UtcNow;
            metadata.AccessCount++;
        }

        public async Task<IEnumerable<string>> SelectEntriesForEvictionAsync(int count, IDictionary<string, CacheEntryMetadata> entries)
        {
            var lruCandidates = (await _lru.SelectEntriesForEvictionAsync(count * 2, entries)).ToList();
            var lfuCandidates = (await _lfu.SelectEntriesForEvictionAsync(count * 2, entries)).ToList();
            
            // Combine scores considering validation category priority and access patterns
            var scoredEntries = entries
                .Where(e => lruCandidates.Contains(e.Key) || lfuCandidates.Contains(e.Key))
                .Select(e => new
                {
                    Key = e.Key,
                    Score = CalculateEvictionScore(e.Value, 
                        lruCandidates.Contains(e.Key),
                        lfuCandidates.Contains(e.Key))
                })
                .OrderByDescending(x => x.Score)
                .Take(count)
                .Select(x => x.Key);

            return scoredEntries;
        }

        public async Task<IDictionary<string, double>> GetMetricsAsync()
        {
            var lruMetrics = await _lru.GetMetricsAsync();
            var lfuMetrics = await _lfu.GetMetricsAsync();
            
            var metrics = new Dictionary<string, double>();
            foreach (var (key, value) in lruMetrics)
                metrics[$"lru_{key}"] = value;
            foreach (var (key, value) in lfuMetrics)
                metrics[$"lfu_{key}"] = value;
                
            return metrics;
        }

        public async Task ResetAsync()
        {
            await _lru.ResetAsync();
            await _lfu.ResetAsync();
            _metadata.Clear();
        }

        private double CalculateEvictionScore(CacheEntryMetadata entry, bool inLruList, bool inLfuList)
        {
            const double AgeWeight = 0.4;
            const double FrequencyWeight = 0.4;
            const double PriorityWeight = 0.2;
            
            double ageScore = inLruList ? 1.0 : 0.0;
            double frequencyScore = inLfuList ? 1.0 : 0.0;
            double priorityScore = Math.Max(0, Math.Min(1.0, entry.Priority / 10.0));
            
            return (ageScore * AgeWeight) + 
                   (frequencyScore * FrequencyWeight) + 
                   (priorityScore * PriorityWeight);
        }
    }
}