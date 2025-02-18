using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public interface IDistributedCache
{
    Task<(bool found, ValidationResult result)> TryGetAsync(string key, ValidationOptions options);
    Task SetAsync(string key, ValidationOptions options, ValidationResult result);
    Task OptimizeDistribution();
    Task InvalidateAsync(string key);
    Task<CacheStats> GetStatisticsAsync();
}

public class CacheStats
{
    public long TotalItems { get; set; }
    public long TotalSizeBytes { get; set; }
    public double HitRate { get; set; }
    public double MissRate { get; set; }
    public double EvictionRate { get; set; }
}