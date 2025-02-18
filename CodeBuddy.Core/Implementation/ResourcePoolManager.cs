using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace CodeBuddy.Core.Implementation
{
    /// <summary>
    /// Centralized manager for various resource pools used in validation operations
    /// </summary>
    public class ResourcePoolManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, object> _pools;
        private bool _isDisposed;
        private static readonly object _syncLock = new object();
        private static ResourcePoolManager _instance;

        public static ResourcePoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncLock)
                    {
                        _instance ??= new ResourcePoolManager();
                    }
                }
                return _instance;
            }
        }

        private ResourcePoolManager()
        {
            _pools = new ConcurrentDictionary<string, object>();
            InitializePools();
        }

        private void InitializePools()
        {
            // Initialize buffer pool
            CreatePool<byte[]>("BufferPool", 
                () => new byte[8192], 
                maxPoolSize: 100);

            // Initialize StringBuilder pool
            CreatePool<StringBuilder>("StringBuilderPool", 
                () => new StringBuilder(1024), 
                cleanup: sb => sb.Clear(),
                maxPoolSize: 50);

            // Initialize MemoryStream pool
            CreatePool<MemoryStream>("MemoryStreamPool", 
                () => new MemoryStream(8192), 
                cleanup: ms => { ms.SetLength(0); ms.Position = 0; },
                maxPoolSize: 50);

            // Initialize StringReader pool
            CreatePool<StringReader>("StringReaderPool", 
                () => new StringReader(string.Empty), 
                maxPoolSize: 30);
        }

        public ResourcePool<T> GetPool<T>(string poolName) where T : class
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ResourcePoolManager));

            if (_pools.TryGetValue(poolName, out var pool))
            {
                return (ResourcePool<T>)pool;
            }

            throw new ArgumentException($"Pool '{poolName}' does not exist.", nameof(poolName));
        }

        private void CreatePool<T>(string poolName, Func<T> factory, Action<T> cleanup = null, int maxPoolSize = 100) where T : class
        {
            var pool = new ResourcePool<T>(factory, cleanup, maxPoolSize);
            _pools.TryAdd(poolName, pool);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_syncLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                foreach (var pool in _pools.Values)
                {
                    if (pool is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _pools.Clear();
            }
        }
    }
}