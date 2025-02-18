using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CodeBuddy.Core.Implementation.CodeValidation.Caching
{
    public class ValidationCache : IValidationCache
    {
        private readonly ValidationCacheConfig _config;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly ValidationCacheStats _stats;
        private readonly IValidationCacheMonitor _monitor;
        private readonly ICacheEvictionStrategy _evictionStrategy;
        private readonly ConcurrentDictionary<string, CacheEntryMetadata> _entryMetadata;
        private readonly Timer _adaptiveAdjustmentTimer;

        public ValidationCache(
            IOptions<ValidationCacheConfig> config,
            IValidationCacheMonitor monitor)
        {
            _config = config.Value;
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _stats = new ValidationCacheStats();
            _monitor = monitor;
            _entryMetadata = new ConcurrentDictionary<string, CacheEntryMetadata>();
            
            // Initialize eviction strategy
            _evictionStrategy = CreateEvictionStrategy(_config.EvictionStrategy);
            
            // Start adaptive adjustment timer if enabled
            if (_config.EnableAdaptiveCaching)
            {
                _adaptiveAdjustmentTimer = new Timer(
                    AdaptiveCacheAdjustment,
                    null,
                    TimeSpan.FromMinutes(_config.AdaptiveAdjustmentIntervalMinutes),
                    TimeSpan.FromMinutes(_config.AdaptiveAdjustmentIntervalMinutes));
            }
        }

        private ICacheEvictionStrategy CreateEvictionStrategy(CacheEvictionStrategy strategy)
        {
            return strategy switch
            {
                CacheEvictionStrategy.LRU => new LRUEvictionStrategy(),
                CacheEvictionStrategy.LFU => new LFUEvictionStrategy(),
                CacheEvictionStrategy.FIFO => new FIFOEvictionStrategy(),
                CacheEvictionStrategy.WeightedCombination => new WeightedCombinationStrategy(),
                _ => new WeightedCombinationStrategy() // Default to weighted combination
            };
        }

        public async Task<(bool found, ValidationResult result)> TryGetAsync(string codeHash, ValidationOptions options)
        {
            var key = GenerateCacheKey(codeHash, options);
            var sw = Stopwatch.StartNew();
            bool found = false;
            
            try
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (IsEntryValid(entry))
                    {
                        _stats.CacheHits++;
                        found = true;
                        await _evictionStrategy.RecordAccessAsync(key, true);
                        UpdateEntryMetadata(key, true);
                        return (true, entry.Result);
                    }
                    
                    // Entry expired
                    await RemoveEntry(key);
                }

                _stats.CacheMisses++;
                await _evictionStrategy.RecordAccessAsync(key, false);
                UpdateEntryMetadata(key, false);
                return (false, null);
            }
            finally
            {
                sw.Stop();
                await _monitor.RecordOperationMetricsAsync($"Cache_Get_{(found ? "Hit" : "Miss")}", sw.ElapsedTicks * 1000000 / Stopwatch.Frequency, found);
            }
        }

        public async Task SetAsync(string codeHash, ValidationOptions options, ValidationResult result)
        {
            var key = GenerateCacheKey(codeHash, options);
            var sw = Stopwatch.StartNew();
            bool success = false;

            try
            {
                var entry = new CacheEntry
                {
                    Result = result,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_config.DefaultTTLMinutes)
                };

                _cache.AddOrUpdate(key, entry, (_, __) => entry);
                success = true;
                await EnforceCacheLimits();
            }
            finally
            {
                sw.Stop();
                await _monitor.RecordOperationMetricsAsync("Cache_Set", sw.ElapsedTicks * 1000000 / Stopwatch.Frequency, success);
            }
        }

        public async Task InvalidateAsync(string reason, string pattern = null)
        {
            var sw = Stopwatch.StartNew();
            int removedCount = 0;

            try
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    removedCount = _cache.Count;
                    _cache.Clear();
                }
                else
                {
                    var keysToRemove = _cache.Keys.Where(k => k.Contains(pattern)).ToList();
                    removedCount = keysToRemove.Count;
                    foreach (var key in keysToRemove)
                    {
                        _cache.TryRemove(key, out _);
                    }
                }

                _stats.InvalidationCount++;
            }
            finally
            {
                sw.Stop();
                await _monitor.RecordOperationMetricsAsync($"Cache_Invalidate_{reason}", sw.ElapsedTicks * 1000000 / Stopwatch.Frequency, true);
                
                var metrics = new CachePerformanceMetrics
                {
                    InvalidationImpactedEntries = removedCount
                };
                await _monitor.AlertIfThresholdExceededAsync(metrics);
            }
        }

        public async Task<ValidationCacheStats> GetStatsAsync()
        {
            _stats.TotalEntries = _cache.Count;
            _stats.CacheSizeBytes = EstimateCacheSize();
            return _stats;
        }

        public async Task MaintenanceAsync()
        {
            var expiredKeys = _cache.Where(kvp => !IsEntryValid(kvp.Value))
                                  .Select(kvp => kvp.Key)
                                  .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            _stats.LastMaintenanceTime = DateTime.UtcNow;
            await EnforceCacheLimits();
        }

        public async Task WarmupAsync()
        {
            if (!_config.EnableCacheWarming)
                return;

            // Implementation would analyze validation patterns and pre-cache common ones
            // This is a placeholder for actual implementation
        }

        private string GenerateCacheKey(string codeHash, ValidationOptions options)
        {
            var input = $"{codeHash}_{options.GetHashCode()}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes);
        }

        private bool IsEntryValid(CacheEntry entry)
        {
            return DateTime.UtcNow < entry.ExpiresAt;
        }

        private long EstimateCacheSize()
        {
            // Rough estimation of cache size
            return _cache.Count * 1024; // Assume 1KB per entry
        }

        private async Task EnforceCacheLimits()
        {
            var currentSize = EstimateCacheSize();
            var maxSize = _config.MaxCacheSizeMB * 1024 * 1024;
            var memoryPressure = (double)currentSize / maxSize * 100;

            // Check if we need to evict entries
            if (_cache.Count > _config.MaxEntries || currentSize > maxSize)
            {
                var entriesToRemove = Math.Max(
                    _cache.Count - _config.MaxEntries,
                    (int)((currentSize - maxSize) / (currentSize / _cache.Count))
                );

                var evictionCandidates = await _evictionStrategy.SelectEntriesForEvictionAsync(
                    entriesToRemove,
                    _entryMetadata
                );

                foreach (var key in evictionCandidates)
                {
                    await RemoveEntry(key);
                }
            }

            // Adaptive cache size adjustment
            if (_config.EnableAdaptiveCaching && memoryPressure > _config.MemoryPressureThresholdPercent)
            {
                _config.MaxEntries = (int)(_config.MaxEntries * 0.9); // Reduce by 10%
                await _monitor.RecordOperationMetricsAsync("Cache_Size_Reduced", 0, true);
            }
        }

        private async Task RemoveEntry(string key)
        {
            _cache.TryRemove(key, out _);
            _entryMetadata.TryRemove(key, out _);
            _stats.EvictionCount++;
        }

        private void UpdateEntryMetadata(string key, bool isHit)
        {
            var metadata = _entryMetadata.GetOrAdd(key, _ => new CacheEntryMetadata());
            metadata.LastAccessedAt = DateTime.UtcNow;
            metadata.AccessCount++;
            metadata.ValidationCategory = "Default"; // This should be set based on validation type
            metadata.Priority = 1; // This should be set based on validation importance
        }

        private async void AdaptiveCacheAdjustment(object state)
        {
            try
            {
                var stats = await GetStatsAsync();
                var hitRatio = stats.HitRatio;

                if (hitRatio < _config.AdaptiveHitRatioThreshold)
                {
                    // If hit ratio is too low, increase cache size
                    _config.MaxEntries = (int)(_config.MaxEntries * 1.1); // Increase by 10%
                    await _monitor.RecordOperationMetricsAsync("Cache_Size_Increased", 0, true);
                }

                // Update eviction strategy metrics
                var strategyMetrics = await _evictionStrategy.GetMetricsAsync();
                await _monitor.RecordStrategyMetricsAsync(_evictionStrategy.Name, strategyMetrics);

                // Predictive preloading if enabled
                if (_config.EnablePredictivePreloading)
                {
                    await PerformPredictivePreloading();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw from timer callback
                await _monitor.RecordOperationMetricsAsync("Cache_Adjustment_Error", 0, false);
            }
        }

        private async Task PerformPredictivePreloading()
        {
            // This is a placeholder for actual implementation
            // It should analyze access patterns and preload likely-to-be-needed entries
            // The actual implementation would depend on your specific use case
        }

        private class CacheEntry
        {
            public ValidationResult Result { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}