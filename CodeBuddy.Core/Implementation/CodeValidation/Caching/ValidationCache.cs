using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models;
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

        public ValidationCache(IOptions<ValidationCacheConfig> config)
        {
            _config = config.Value;
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _stats = new ValidationCacheStats();
        }

        public async Task<(bool found, ValidationResult result)> TryGetAsync(string codeHash, ValidationOptions options)
        {
            var key = GenerateCacheKey(codeHash, options);
            
            if (_cache.TryGetValue(key, out var entry))
            {
                if (IsEntryValid(entry))
                {
                    _stats.CacheHits++;
                    return (true, entry.Result);
                }
                
                // Entry expired
                _cache.TryRemove(key, out _);
            }

            _stats.CacheMisses++;
            return (false, null);
        }

        public async Task SetAsync(string codeHash, ValidationOptions options, ValidationResult result)
        {
            var key = GenerateCacheKey(codeHash, options);
            var entry = new CacheEntry
            {
                Result = result,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_config.DefaultTTLMinutes)
            };

            _cache.AddOrUpdate(key, entry, (_, __) => entry);
            await EnforceCacheLimits();
        }

        public async Task InvalidateAsync(string reason, string pattern = null)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                _cache.Clear();
            }
            else
            {
                var keysToRemove = _cache.Keys.Where(k => k.Contains(pattern));
                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }

            _stats.InvalidationCount++;
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