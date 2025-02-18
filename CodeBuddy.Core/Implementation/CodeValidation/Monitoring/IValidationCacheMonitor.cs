using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IValidationCacheMonitor
    {
        /// <summary>
        /// Records metrics for cache operations
        /// </summary>
        Task RecordOperationMetricsAsync(string operation, long latencyMicroseconds, bool success);

        /// <summary>
        /// Checks if any performance thresholds have been exceeded and raises alerts
        /// </summary>
        Task AlertIfThresholdExceededAsync(CachePerformanceMetrics metrics);

        /// <summary>
        /// Records metrics specific to the eviction strategy
        /// </summary>
        Task RecordStrategyMetricsAsync(string strategy, IDictionary<string, double> metrics);

        /// <summary>
        /// Gets effectiveness metrics for each eviction strategy
        /// </summary>
        Task<IDictionary<string, double>> GetStrategyEffectivenessAsync();

        /// <summary>
        /// Updates metrics for adaptive cache sizing
        /// </summary>
        Task UpdateAdaptiveCacheMetricsAsync(double hitRatio, double memoryPressure);
    }
}