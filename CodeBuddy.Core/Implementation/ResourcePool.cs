using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace CodeBuddy.Core.Implementation
{
    /// <summary>
    /// Generic resource pool that manages reusable resources with automatic scaling and cleanup
    /// </summary>
    /// <typeparam name="T">Type of resource to pool</typeparam>
    public enum ResourcePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public class ResourcePool<T> : IDisposable where T : class
    {
        private readonly ConcurrentDictionary<ResourcePriority, ConcurrentQueue<T>> _available;
        private readonly ConcurrentDictionary<T, (DateTime timestamp, ResourcePriority priority)> _inUse;
        private readonly Func<T> _factory;
        private readonly Action<T> _cleanup;
        private readonly TimeSpan _maxIdleTime;
        private readonly int _maxPoolSize;
        private readonly Timer _cleanupTimer;
        private bool _isDisposed;
        private readonly object _syncLock = new object();

        public ResourcePool(Func<T> factory, Action<T> cleanup = null, int maxPoolSize = 100, TimeSpan? maxIdleTime = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _cleanup = cleanup;
            _maxPoolSize = maxPoolSize;
            _maxIdleTime = maxIdleTime ?? TimeSpan.FromMinutes(10);
            _available = new ConcurrentDictionary<ResourcePriority, ConcurrentQueue<T>>();
            foreach (ResourcePriority priority in Enum.GetValues(typeof(ResourcePriority)))
            {
                _available[priority] = new ConcurrentQueue<T>();
            }
            _inUse = new ConcurrentDictionary<T, (DateTime, ResourcePriority)>();
            
            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public T Acquire(ResourcePriority priority = ResourcePriority.Normal)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ResourcePool<T>));

            T item;
            // Try to get from requested priority level first
            if (!TryGetFromQueue(_available[priority], out item))
            {
                // If not available, try higher priority queues
                for (int p = (int)priority + 1; p <= (int)ResourcePriority.Critical; p++)
                {
                    if (TryGetFromQueue(_available[(ResourcePriority)p], out item))
                        break;
                }

                // If still not found, try lower priority queues
                if (item == null)
                {
                    for (int p = (int)priority - 1; p >= (int)ResourcePriority.Low; p--)
                    {
                        if (TryGetFromQueue(_available[(ResourcePriority)p], out item))
                            break;
                    }
                }
            }

            // If no item is available in any queue, create new or wait
            if (item == null)
            {
                if (Count >= _maxPoolSize)
                {
                    // Wait for an item to become available
                    SpinWait.SpinUntil(() => _available.TryTake(out item) || Count < _maxPoolSize);
                }

                if (item == null && Count < _maxPoolSize)
                {
                    item = _factory();
                }
            }

            if (item != null)
            {
                _inUse[item] = (DateTime.UtcNow, priority);
            }

            return item;
        }

        public void Release(T item, ResourcePriority? newPriority = null)
        {
            if (item == null) return;

            DateTime value;
            if (_inUse.TryRemove(item, out var value))
            {
                var priority = newPriority ?? value.priority;
                _available[priority].Enqueue(item);
            }
        }

        public int Count => _available.Values.Sum(q => q.Count) + _inUse.Count;
        public int AvailableCount => _available.Values.Sum(q => q.Count);
        public int InUseCount => _inUse.Count;

        private void CleanupCallback(object state)
        {
            if (_isDisposed) return;

            var now = DateTime.UtcNow;
            var itemsToRemove = _inUse
                .Where(kvp => now - kvp.Value.timestamp > _maxIdleTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                if (_inUse.TryRemove(item, out _))
                {
                    CleanupItem(item);
                }
            }

            // Cleanup idle available items if pool is too large
            // Clean up starting from lowest priority
            foreach (ResourcePriority priority in Enum.GetValues(typeof(ResourcePriority)))
            {
                while (_available[priority].Count > _maxPoolSize / _available.Count)
                {
                    if (_available[priority].TryDequeue(out var item))
                    {
                        CleanupItem(item);
                    }
                }
            }
                {
                    CleanupItem(item);
                }
            }
        }

        private void CleanupItem(T item)
        {
            try
            {
                _cleanup?.Invoke(item);
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Suppress cleanup errors
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_syncLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _cleanupTimer?.Dispose();

                // Cleanup all items
                foreach (var priorityQueue in _available.Values)
                {
                    while (priorityQueue.TryDequeue(out var item))
                    {
                        CleanupItem(item);
                    }
                }
                {
                    CleanupItem(item);
                }

                foreach (var item in _inUse.Keys)
                {
                    CleanupItem(item);
                }

                _inUse.Clear();
            }
        }

        private bool TryGetFromQueue(ConcurrentQueue<T> queue, out T item)
        {
            return queue.TryDequeue(out item);
        }

        public void UpdatePriority(T item, ResourcePriority newPriority)
        {
            if (_inUse.TryGetValue(item, out var value))
            {
                _inUse[item] = (value.timestamp, newPriority);
            }
        }

        public Dictionary<ResourcePriority, int> GetPriorityDistribution()
        {
            return _available.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count
            );
        }

        public (int total, int available, int inUse) GetPoolStatistics()
        {
            int available = _available.Values.Sum(q => q.Count);
            int inUse = _inUse.Count;
            return (available + inUse, available, inUse);
        }

        public double GetUtilizationRate()
        {
            var stats = GetPoolStatistics();
            return stats.total == 0 ? 0 : (double)stats.inUse / stats.total;
        }
    }
}