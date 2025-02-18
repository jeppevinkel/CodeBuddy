using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.MemoryManagement;

/// <summary>
/// Manages a pool of ValidationContext objects for efficient reuse during validation operations.
/// </summary>
public class ValidationContextPool : IDisposable
{
    private readonly ObjectPool<ValidationContext> _contextPool;
    private readonly MemoryPoolManager _memoryManager;
    private int _activeContexts;
    private bool _disposed;

    public ValidationContextPool(MemoryPoolManager memoryManager)
    {
        _memoryManager = memoryManager;
        _contextPool = _memoryManager.GetPool<ValidationContext>();
    }

    /// <summary>
    /// Gets a ValidationContext from the pool or creates a new one if needed.
    /// </summary>
    public ValidationContext Rent()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ValidationContextPool));

        var context = _contextPool.Get();
        Interlocked.Increment(ref _activeContexts);
        return context;
    }

    /// <summary>
    /// Returns a ValidationContext to the pool for reuse.
    /// </summary>
    public void Return(ValidationContext context)
    {
        if (_disposed) return;

        if (context != null)
        {
            context.Reset(); // Reset state for reuse
            _contextPool.Return(context);
            Interlocked.Decrement(ref _activeContexts);
        }
    }

    /// <summary>
    /// Gets the number of currently active (checked out) contexts.
    /// </summary>
    public int ActiveContexts => _activeContexts;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clear pool - specific cleanup if needed
        while (_activeContexts > 0)
        {
            Thread.Sleep(10); // Wait for active contexts to be returned
        }
    }
}