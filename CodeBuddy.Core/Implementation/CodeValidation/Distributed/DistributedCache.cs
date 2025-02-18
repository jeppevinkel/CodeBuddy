using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Distributed;

public class DistributedCache : IDistributedCache
{
    private readonly ILogger<DistributedCache> _logger;
    private readonly IClusterCoordinator _clusterCoordinator;
    private readonly ConcurrentDictionary<string, CacheEntry> _localCache;
    private readonly ConsistentHash<string> _hashRing;
    private readonly CacheStats _stats;
    private readonly object _statsLock = new();

    public DistributedCache(
        ILogger<DistributedCache> logger,
        IClusterCoordinator clusterCoordinator)
    {
        _logger = logger;
        _clusterCoordinator = clusterCoordinator;
        _localCache = new ConcurrentDictionary<string, CacheEntry>();
        _hashRing = new ConsistentHash<string>();
        _stats = new CacheStats();
    }

    public async Task<(bool found, ValidationResult result)> TryGetAsync(string key, ValidationOptions options)
    {
        var responsibleNode = _hashRing.GetNode(key);
        
        // Check if we're responsible for this key
        if (responsibleNode == _clusterCoordinator.CurrentNodeId)
        {
            return await GetFromLocalCache(key, options);
        }

        // Forward request to responsible node
        try
        {
            return await ForwardCacheRequest(responsibleNode, key, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward cache request to node {NodeId}", responsibleNode);
            
            // Fallback to local cache in case of network issues
            return await GetFromLocalCache(key, options);
        }
    }

    public async Task SetAsync(string key, ValidationOptions options, ValidationResult result)
    {
        var responsibleNode = _hashRing.GetNode(key);
        
        if (responsibleNode == _clusterCoordinator.CurrentNodeId)
        {
            await SetInLocalCache(key, options, result);
        }
        else
        {
            try
            {
                await ForwardCacheSet(responsibleNode, key, options, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward cache set to node {NodeId}", responsibleNode);
                
                // Store locally as backup
                await SetInLocalCache(key, options, result);
            }
        }
    }

    public async Task OptimizeDistribution()
    {
        try
        {
            var nodes = await _clusterCoordinator.GetActiveNodes();
            
            // Update hash ring with current nodes
            _hashRing.UpdateNodes(nodes.Select(n => n.NodeId));

            // Rebalance cache entries if needed
            var entriesToMove = _localCache
                .Where(kvp => _hashRing.GetNode(kvp.Key) != _clusterCoordinator.CurrentNodeId)
                .ToList();

            foreach (var entry in entriesToMove)
            {
                var targetNode = _hashRing.GetNode(entry.Key);
                try
                {
                    await ForwardCacheSet(targetNode, entry.Key, entry.Value.Options, entry.Value.Result);
                    _localCache.TryRemove(entry.Key, out _);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move cache entry {Key} to node {NodeId}", entry.Key, targetNode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize cache distribution");
        }
    }

    public async Task InvalidateAsync(string key)
    {
        var responsibleNode = _hashRing.GetNode(key);
        
        if (responsibleNode == _clusterCoordinator.CurrentNodeId)
        {
            _localCache.TryRemove(key, out _);
        }
        else
        {
            try
            {
                await ForwardCacheInvalidation(responsibleNode, key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward cache invalidation to node {NodeId}", responsibleNode);
            }
        }
    }

    public Task<CacheStats> GetStatisticsAsync()
    {
        lock (_statsLock)
        {
            _stats.TotalItems = _localCache.Count;
            _stats.TotalSizeBytes = _localCache.Sum(kvp => GetEntrySizeBytes(kvp.Value));
            return Task.FromResult(_stats);
        }
    }

    private async Task<(bool found, ValidationResult result)> GetFromLocalCache(string key, ValidationOptions options)
    {
        if (_localCache.TryGetValue(key, out var entry))
        {
            if (IsEntryValid(entry, options))
            {
                IncrementHits();
                return (true, entry.Result);
            }
            
            // Entry expired or invalid
            _localCache.TryRemove(key, out _);
        }

        IncrementMisses();
        return (false, null);
    }

    private async Task SetInLocalCache(string key, ValidationOptions options, ValidationResult result)
    {
        var entry = new CacheEntry
        {
            Result = result,
            Options = options,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow
        };

        _localCache[key] = entry;

        // Perform cache eviction if needed
        await EvictEntriesIfNeeded();
    }

    private bool IsEntryValid(CacheEntry entry, ValidationOptions options)
    {
        if (options.MaxCacheAge.HasValue)
        {
            var age = DateTime.UtcNow - entry.Created;
            if (age > options.MaxCacheAge.Value)
            {
                return false;
            }
        }

        return true;
    }

    private async Task EvictEntriesIfNeeded()
    {
        var currentSize = _localCache.Sum(kvp => GetEntrySizeBytes(kvp.Value));
        if (currentSize > MaxCacheSizeBytes)
        {
            var entriesToEvict = _localCache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take((int)(_localCache.Count * 0.2)) // Evict 20% of entries
                .ToList();

            foreach (var entry in entriesToEvict)
            {
                _localCache.TryRemove(entry.Key, out _);
                IncrementEvictions();
            }
        }
    }

    private long GetEntrySizeBytes(CacheEntry entry)
    {
        // Rough estimation of entry size
        return 1000; // Placeholder
    }

    private void IncrementHits()
    {
        lock (_statsLock)
        {
            _stats.HitRate = (_stats.HitRate * _stats.TotalItems + 1) / (_stats.TotalItems + 1);
        }
    }

    private void IncrementMisses()
    {
        lock (_statsLock)
        {
            _stats.MissRate = (_stats.MissRate * _stats.TotalItems + 1) / (_stats.TotalItems + 1);
        }
    }

    private void IncrementEvictions()
    {
        lock (_statsLock)
        {
            _stats.EvictionRate = (_stats.EvictionRate * _stats.TotalItems + 1) / (_stats.TotalItems + 1);
        }
    }

    private class CacheEntry
    {
        public ValidationResult Result { get; set; }
        public ValidationOptions Options { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccessed { get; set; }
    }

    private const long MaxCacheSizeBytes = 1024 * 1024 * 1024; // 1GB

    private Task<(bool found, ValidationResult result)> ForwardCacheRequest(string nodeId, string key, ValidationOptions options)
    {
        throw new NotImplementedException();
    }

    private Task ForwardCacheSet(string nodeId, string key, ValidationOptions options, ValidationResult result)
    {
        throw new NotImplementedException();
    }

    private Task ForwardCacheInvalidation(string nodeId, string key)
    {
        throw new NotImplementedException();
    }
}

public class ConsistentHash<T>
{
    private readonly SortedDictionary<int, T> _circle = new();
    private const int NumberOfReplicas = 100;

    public void UpdateNodes(IEnumerable<T> nodes)
    {
        _circle.Clear();
        foreach (var node in nodes)
        {
            for (int i = 0; i < NumberOfReplicas; i++)
            {
                var hash = GetHash($"{node}:{i}");
                _circle[hash] = node;
            }
        }
    }

    public T GetNode(string key)
    {
        if (_circle.Count == 0)
        {
            throw new InvalidOperationException("No nodes in hash ring");
        }

        var hash = GetHash(key);
        var entry = _circle.FirstOrDefault(kvp => kvp.Key >= hash);
        return entry.Equals(default(KeyValuePair<int, T>)) ? _circle.First().Value : entry.Value;
    }

    private int GetHash(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(hash, 0);
    }
}