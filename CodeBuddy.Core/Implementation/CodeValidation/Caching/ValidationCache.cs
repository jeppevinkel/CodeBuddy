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

        public ValidationCache(
            IOptions<ValidationCacheConfig> config,
            IValidationCacheMonitor monitor)
        {
            _config = config.Value;
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _stats = new ValidationCacheStats();
            _monitor = monitor;
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
                        return (true, entry.Result);
                    }
                    
                    // Entry expired
                    _cache.TryRemove(key, out _);
                }

                _stats.CacheMisses++;
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
            while (_cache.Count > _config.MaxEntries || EstimateCacheSize() > _config.MaxCacheSizeMB * 1024 * 1024)
            {
                // Remove oldest entries first
                var oldestEntry = _cache.OrderBy(kvp => kvp.Value.CreatedAt).FirstOrDefault();
                if (!string.IsNullOrEmpty(oldestEntry.Key))
                {
                    _cache.TryRemove(oldestEntry.Key, out _);
                }
                else
                {
                    break;
                }
            }
        }

        private class CacheEntry
        {
            public ValidationResult Result { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}