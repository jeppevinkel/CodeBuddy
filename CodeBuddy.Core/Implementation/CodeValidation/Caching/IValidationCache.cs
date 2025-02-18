using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Caching
{
    public interface IValidationCache
    {
        /// <summary>
        /// Tries to get a cached validation result
        /// </summary>
        Task<(bool found, ValidationResult result)> TryGetAsync(string codeHash, ValidationOptions options);

        /// <summary>
        /// Stores a validation result in the cache
        /// </summary>
        Task SetAsync(string codeHash, ValidationOptions options, ValidationResult result);

        /// <summary>
        /// Invalidates cache entries based on specified criteria
        /// </summary>
        Task InvalidateAsync(string reason, string pattern = null);

        /// <summary>
        /// Gets current cache statistics
        /// </summary>
        Task<ValidationCacheStats> GetStatsAsync();

        /// <summary>
        /// Performs cache maintenance operations
        /// </summary>
        Task MaintenanceAsync();

        /// <summary>
        /// Warms up cache with frequently used patterns
        /// </summary>
        Task WarmupAsync();
    }

    public class ValidationCacheStats
    {
        public int TotalEntries { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double HitRatio => CacheHits + CacheMisses == 0 ? 0 : (double)CacheHits / (CacheHits + CacheMisses);
        public long CacheSizeBytes { get; set; }
        public int PartialUpdateCount { get; set; }
        public int InvalidationCount { get; set; }
        public DateTime LastMaintenanceTime { get; set; }
    }
}