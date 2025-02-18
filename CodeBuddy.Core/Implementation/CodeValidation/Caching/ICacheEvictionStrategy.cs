using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation.Caching
{
    public interface ICacheEvictionStrategy
    {
        /// <summary>
        /// Gets the name of the eviction strategy
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Records an access to a cache entry
        /// </summary>
        Task RecordAccessAsync(string key, bool isHit);

        /// <summary>
        /// Selects entries for eviction when cache limits are reached
        /// </summary>
        Task<IEnumerable<string>> SelectEntriesForEvictionAsync(int count, IDictionary<string, CacheEntryMetadata> entries);

        /// <summary>
        /// Gets strategy-specific statistics
        /// </summary>
        Task<IDictionary<string, double>> GetMetricsAsync();

        /// <summary>
        /// Initializes or resets the strategy
        /// </summary>
        Task ResetAsync();
    }

    public class CacheEntryMetadata
    {
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
        public long SizeInBytes { get; set; }
        public string ValidationCategory { get; set; }
        public int Priority { get; set; }
    }
}