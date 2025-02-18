using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.MemoryManagement;

/// <summary>
/// Manages multi-level memory pools for different types of validation objects
/// with optimized memory utilization and automatic pressure-based scaling.
/// </summary>
public class MemoryPoolManager : IDisposable, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Type, ObjectPool<object>> _objectPools;
    private readonly ConcurrentDictionary<int, ObjectPool<byte[]>> _bufferPools;
    private readonly ConcurrentDictionary<string, PoolStatistics> _poolStats;
    private readonly SemaphoreSlim _trimLock;
    private readonly Timer _monitoringTimer;
    private readonly MemoryPressureMonitor _pressureMonitor;
    private bool _disposed;

    // Pool configuration
    private const int DefaultPoolSize = 100;
    private const int MaxPoolSize = 1000;
    private const int BufferPoolSizeIncrement = 1024; // 1KB increments
    private const int MaxBufferSize = 16 * 1024 * 1024; // 16MB

    // Memory pressure thresholds
    private const long LowPressureThreshold = 300 * 1024 * 1024;   // 300MB
    private const long MediumPressureThreshold = 500 * 1024 * 1024;  // 500MB
    private const long HighPressureThreshold = 700 * 1024 * 1024;   // 700MB
    private const long CriticalPressureThreshold = 900 * 1024 * 1024; // 900MB

    public MemoryPoolManager(ILogger logger)
    {
        _logger = logger;
        _objectPools = new ConcurrentDictionary<Type, ObjectPool<object>>();
        _bufferPools = new ConcurrentDictionary<int, ObjectPool<byte[]>>();
        _poolStats = new ConcurrentDictionary<string, PoolStatistics>();
        _trimLock = new SemaphoreSlim(1, 1);
        _pressureMonitor = new MemoryPressureMonitor(HandleMemoryPressure);
        _monitoringTimer = new Timer(MonitorPools, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Gets or creates a pool for objects of the specified type.
    /// </summary>
    public ObjectPool<T> GetPool<T>() where T : class, new()
    {
        var type = typeof(T);
        var pool = _objectPools.GetOrAdd(type, _ => CreatePool<T>());
        return (ObjectPool<T>)(object)pool;
    }

    /// <summary>
    /// Gets or creates a buffer pool for the specified size.
    /// </summary>
    public ObjectPool<byte[]> GetBufferPool(int bufferSize)
    {
        bufferSize = NormalizeBufferSize(bufferSize);
        return _bufferPools.GetOrAdd(bufferSize, size => CreateBufferPool(size));
    }

    private ObjectPool<T> CreatePool<T>() where T : class, new()
    {
        var policy = new PooledObjectPolicy<T>(
            createFunc: () => new T(),
            maxPoolSize: DefaultPoolSize);

        var poolName = typeof(T).Name;
        _poolStats.TryAdd(poolName, new PoolStatistics());

        return new ObjectPool<T>(policy);
    }

    private ObjectPool<byte[]> CreateBufferPool(int bufferSize)
    {
        var policy = new PooledObjectPolicy<byte[]>(
            createFunc: () => new byte[bufferSize],
            maxPoolSize: CalculateBufferPoolSize(bufferSize));

        var poolName = $"Buffer_{bufferSize}";
        _poolStats.TryAdd(poolName, new PoolStatistics());

        return new ObjectPool<byte[]>(policy);
    }

    private int CalculateBufferPoolSize(int bufferSize)
    {
        // Adjust pool size based on buffer size - smaller buffers get larger pools
        if (bufferSize <= 4096) return MaxPoolSize;
        if (bufferSize <= 16384) return MaxPoolSize / 2;
        if (bufferSize <= 65536) return MaxPoolSize / 4;
        return MaxPoolSize / 8;
    }

    private int NormalizeBufferSize(int requestedSize)
    {
        // Round up to the nearest increment
        var size = ((requestedSize + BufferPoolSizeIncrement - 1) / BufferPoolSizeIncrement) * BufferPoolSizeIncrement;
        return Math.Min(size, MaxBufferSize);
    }

    private void MonitorPools(object state)
    {
        if (_disposed) return;

        try
        {
            foreach (var (poolName, stats) in _poolStats)
            {
                stats.UpdateUtilization();
                
                if (stats.UtilizationPercent < 20 && stats.Age > TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation(
                        "Low utilization detected for pool {PoolName}: {Utilization}%. Scheduling trim.",
                        poolName, stats.UtilizationPercent);
                    
                    Task.Run(() => TrimPoolAsync(poolName));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring pools");
        }
    }

    private async Task TrimPoolAsync(string poolName)
    {
        if (!await _trimLock.WaitAsync(TimeSpan.FromSeconds(1)))
            return;

        try
        {
            if (_poolStats.TryGetValue(poolName, out var stats))
            {
                if (poolName.StartsWith("Buffer_"))
                {
                    var size = int.Parse(poolName.Substring(7));
                    if (_bufferPools.TryGetValue(size, out var pool))
                    {
                        ((IPooledObjectPolicy)pool).TrimExcess();
                    }
                }
                else
                {
                    var type = Type.GetType(poolName);
                    if (type != null && _objectPools.TryGetValue(type, out var pool))
                    {
                        ((IPooledObjectPolicy)pool).TrimExcess();
                    }
                }

                stats.Reset();
            }
        }
        finally
        {
            _trimLock.Release();
        }
    }

    private void HandleMemoryPressure(MemoryPressureLevel level)
    {
        switch (level)
        {
            case MemoryPressureLevel.Low:
                TrimUnderutilizedPools();
                break;

            case MemoryPressureLevel.Medium:
                TrimAllPools(0.5);
                break;

            case MemoryPressureLevel.High:
                TrimAllPools(0.75);
                break;

            case MemoryPressureLevel.Critical:
                ClearAllPools();
                break;
        }
    }

    private void TrimUnderutilizedPools()
    {
        foreach (var (poolName, stats) in _poolStats)
        {
            if (stats.UtilizationPercent < 30)
            {
                Task.Run(() => TrimPoolAsync(poolName));
            }
        }
    }

    private void TrimAllPools(double trimPercentage)
    {
        foreach (var pool in _bufferPools.Values)
        {
            ((IPooledObjectPolicy)pool).TrimExcess(trimPercentage);
        }

        foreach (var pool in _objectPools.Values)
        {
            ((IPooledObjectPolicy)pool).TrimExcess(trimPercentage);
        }
    }

    private void ClearAllPools()
    {
        foreach (var pool in _bufferPools.Values)
        {
            ((IPooledObjectPolicy)pool).Clear();
        }

        foreach (var pool in _objectPools.Values)
        {
            ((IPooledObjectPolicy)pool).Clear();
        }

        GC.Collect(2, GCCollectionMode.Aggressive, true);
    }

    public Dictionary<string, PoolStatistics> GetStatistics()
    {
        return new Dictionary<string, PoolStatistics>(_poolStats);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _monitoringTimer?.Dispose();
        _pressureMonitor?.Dispose();
        _trimLock?.Dispose();
        ClearAllPools();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        _monitoringTimer?.Dispose();
        _pressureMonitor?.Dispose();

        await _trimLock.WaitAsync();
        try
        {
            ClearAllPools();
        }
        finally
        {
            _trimLock.Dispose();
        }
    }

    private class MemoryPressureMonitor : IDisposable
    {
        private readonly Action<MemoryPressureLevel> _onPressureChanged;
        private readonly Timer _timer;
        private bool _disposed;

        public MemoryPressureMonitor(Action<MemoryPressureLevel> onPressureChanged)
        {
            _onPressureChanged = onPressureChanged;
            _timer = new Timer(CheckMemoryPressure, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void CheckMemoryPressure(object state)
        {
            if (_disposed) return;

            var currentMemory = Process.GetCurrentProcess().WorkingSet64;
            var level = GetPressureLevel(currentMemory);
            _onPressureChanged(level);
        }

        private MemoryPressureLevel GetPressureLevel(long currentMemory)
        {
            if (currentMemory >= CriticalPressureThreshold) return MemoryPressureLevel.Critical;
            if (currentMemory >= HighPressureThreshold) return MemoryPressureLevel.High;
            if (currentMemory >= MediumPressureThreshold) return MemoryPressureLevel.Medium;
            if (currentMemory >= LowPressureThreshold) return MemoryPressureLevel.Low;
            return MemoryPressureLevel.None;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
        }
    }

    private class PooledObjectPolicy<T> : IPooledObjectPolicy<T> where T : class
    {
        private readonly Func<T> _createFunc;
        private readonly int _maxPoolSize;
        private int _currentSize;

        public PooledObjectPolicy(Func<T> createFunc, int maxPoolSize)
        {
            _createFunc = createFunc;
            _maxPoolSize = maxPoolSize;
        }

        public T Create() => _createFunc();

        public bool Return(T obj)
        {
            if (Interlocked.Increment(ref _currentSize) <= _maxPoolSize)
                return true;

            Interlocked.Decrement(ref _currentSize);
            return false;
        }

        public void TrimExcess(double percentage = 1.0)
        {
            var targetSize = (int)(_maxPoolSize * (1 - percentage));
            while (_currentSize > targetSize)
            {
                Interlocked.Decrement(ref _currentSize);
            }
        }

        public void Clear()
        {
            _currentSize = 0;
        }
    }

    private interface IPooledObjectPolicy
    {
        void TrimExcess(double percentage = 1.0);
        void Clear();
    }

    public class PoolStatistics
    {
        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private long _totalRequests;
        private long _currentItems;
        private long _peakItems;
        private readonly object _lock = new();

        public TimeSpan Age => _uptime.Elapsed;
        public long TotalRequests => _totalRequests;
        public long CurrentItems => _currentItems;
        public long PeakItems => _peakItems;
        public double UtilizationPercent { get; private set; }

        public void IncrementRequests()
        {
            Interlocked.Increment(ref _totalRequests);
        }

        public void UpdateItemCount(long count)
        {
            lock (_lock)
            {
                _currentItems = count;
                if (count > _peakItems)
                    _peakItems = count;
            }
        }

        public void UpdateUtilization()
        {
            lock (_lock)
            {
                if (_peakItems > 0)
                {
                    UtilizationPercent = (_currentItems / (double)_peakItems) * 100;
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _currentItems = 0;
                _peakItems = 0;
                _totalRequests = 0;
                UtilizationPercent = 0;
                _uptime.Restart();
            }
        }
    }

    private enum MemoryPressureLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }
}