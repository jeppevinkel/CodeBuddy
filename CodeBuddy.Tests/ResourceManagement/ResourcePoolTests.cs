using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Xunit;
using CodeBuddy.Core.Implementation;

namespace CodeBuddy.Tests.ResourceManagement
{
    public class ResourcePoolTests
    {
        [Fact]
        public void ResourcePool_CreateAndDispose_Success()
        {
            using var pool = new ResourcePool<byte[]>(() => new byte[1024]);
            Assert.NotNull(pool);
        }

        [Fact]
        public void ResourcePool_AcquireAndRelease_Success()
        {
            using var pool = new ResourcePool<byte[]>(() => new byte[1024]);
            
            var resource = pool.Acquire();
            Assert.NotNull(resource);
            Assert.Equal(1, pool.InUseCount);
            Assert.Equal(0, pool.AvailableCount);
            
            pool.Release(resource);
            Assert.Equal(0, pool.InUseCount);
            Assert.Equal(1, pool.AvailableCount);
        }

        [Fact]
        public async Task ResourcePool_ConcurrentAccess_Success()
        {
            using var pool = new ResourcePool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear());
            var tasks = new List<Task>();
            
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var sb = pool.Acquire();
                    sb.Append("Test");
                    Task.Delay(10).Wait(); // Simulate work
                    pool.Release(sb);
                }));
            }
            
            await Task.WhenAll(tasks);
            Assert.Equal(0, pool.InUseCount);
            Assert.True(pool.AvailableCount > 0);
        }

        [Fact]
        public void ResourcePool_MaxPoolSize_Respected()
        {
            using var pool = new ResourcePool<MemoryStream>(() => new MemoryStream(), maxPoolSize: 5);
            var resources = new List<MemoryStream>();
            
            // Acquire up to max size
            for (int i = 0; i < 5; i++)
            {
                resources.Add(pool.Acquire());
            }
            
            Assert.Equal(5, pool.Count);
            
            // Release all
            foreach (var resource in resources)
            {
                pool.Release(resource);
            }
            
            Assert.Equal(5, pool.AvailableCount);
        }

        [Fact]
        public void ResourcePool_Cleanup_Success()
        {
            var cleanupCalled = 0;
            using (var pool = new ResourcePool<MemoryStream>(
                () => new MemoryStream(), 
                cleanup: ms => { ms.SetLength(0); cleanupCalled++; }))
            {
                var ms = pool.Acquire();
                ms.WriteByte(1);
                pool.Release(ms);
            }
            
            Assert.True(cleanupCalled > 0);
        }

        [Fact]
        public void ResourcePoolManager_GetPool_Success()
        {
            var manager = ResourcePoolManager.Instance;
            
            var bufferPool = manager.GetPool<byte[]>("BufferPool");
            Assert.NotNull(bufferPool);
            
            var stringBuilderPool = manager.GetPool<StringBuilder>("StringBuilderPool");
            Assert.NotNull(stringBuilderPool);
            
            var memoryStreamPool = manager.GetPool<MemoryStream>("MemoryStreamPool");
            Assert.NotNull(memoryStreamPool);
        }

        [Fact]
        public async Task ResourcePoolManager_ConcurrentPoolAccess_Success()
        {
            var manager = ResourcePoolManager.Instance;
            var tasks = new List<Task>();
            
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var bufferPool = manager.GetPool<byte[]>("BufferPool");
                    var buffer = bufferPool.Acquire();
                    Task.Delay(10).Wait(); // Simulate work
                    bufferPool.Release(buffer);

                    var sbPool = manager.GetPool<StringBuilder>("StringBuilderPool");
                    var sb = sbPool.Acquire();
                    Task.Delay(10).Wait(); // Simulate work
                    sbPool.Release(sb);
                }));
            }
            
            await Task.WhenAll(tasks);
            
            var bufferPool = manager.GetPool<byte[]>("BufferPool");
            var sbPool = manager.GetPool<StringBuilder>("StringBuilderPool");
            
            Assert.Equal(0, bufferPool.InUseCount);
            Assert.Equal(0, sbPool.InUseCount);
        }
    }
}