using System;

namespace CodeBuddy.Core.Models
{
    public class ValidationCacheConfig
    {
        /// <summary>
        /// Maximum size of the cache in megabytes
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 1024; // 1GB default

        /// <summary>
        /// Default Time-To-Live for cache entries in minutes
        /// </summary>
        public int DefaultTTLMinutes { get; set; } = 60;

        /// <summary>
        /// Whether to enable distributed caching
        /// </summary>
        public bool EnableDistributedCache { get; set; } = false;

        /// <summary>
        /// Whether to enable cache warming
        /// </summary>
        public bool EnableCacheWarming { get; set; } = false;

        /// <summary>
        /// Maximum number of entries to keep in the cache
        /// </summary>
        public int MaxEntries { get; set; } = 10000;

        /// <summary>
        /// Minimum validation frequency required for cache warming
        /// </summary>
        public int CacheWarmingThreshold { get; set; } = 5;

        /// <summary>
        /// Whether to enable partial cache updates
        /// </summary>
        public bool EnablePartialUpdates { get; set; } = true;
    }
}