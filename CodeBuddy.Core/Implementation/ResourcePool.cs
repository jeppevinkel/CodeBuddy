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
    public class ResourcePool<T> : IDisposable where T : class
    {
        private readonly ConcurrentBag<T> _available;
        private readonly ConcurrentDictionary<T, DateTime> _inUse;
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
            _available = new ConcurrentBag<T>();
            _inUse = new ConcurrentDictionary<T, DateTime>();
            
            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public T Acquire()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ResourcePool<T>));

            T item;
            if (!_available.TryTake(out item))
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
                _inUse[item] = DateTime.UtcNow;
            }

            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;

            DateTime value;
            if (_inUse.TryRemove(item, out value))
            {
                _available.Add(item);
            }
        }

        public int Count => _available.Count + _inUse.Count;
        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;

        private void CleanupCallback(object state)
        {
            if (_isDisposed) return;

            var now = DateTime.UtcNow;
            var itemsToRemove = _inUse
                .Where(kvp => now - kvp.Value > _maxIdleTime)
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
            while (_available.Count > _maxPoolSize)
            {
                if (_available.TryTake(out var item))
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
                while (_available.TryTake(out var item))
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
    }
}