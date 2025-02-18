using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// A thread-safe priority queue implementation
    /// </summary>
    internal class ConcurrentPriorityQueue<T>
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<T>> _queues;
        private readonly SortedSet<int> _priorities;
        private readonly ReaderWriterLockSlim _lock;
        private int _count;

        public ConcurrentPriorityQueue()
        {
            _queues = new ConcurrentDictionary<int, ConcurrentQueue<T>>();
            _priorities = new SortedSet<int>();
            _lock = new ReaderWriterLockSlim();
        }

        public int Count => _count;

        public void Enqueue(T item, int priority)
        {
            _lock.EnterWriteLock();
            try
            {
                var queue = _queues.GetOrAdd(priority, _ => new ConcurrentQueue<T>());
                queue.Enqueue(item);
                _priorities.Add(priority);
                Interlocked.Increment(ref _count);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryDequeue(out T item)
        {
            item = default;
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_priorities.Count == 0)
                {
                    return false;
                }

                _lock.EnterWriteLock();
                try
                {
                    var highestPriority = _priorities.Min;
                    if (_queues.TryGetValue(highestPriority, out var queue))
                    {
                        if (queue.TryDequeue(out item))
                        {
                            Interlocked.Decrement(ref _count);
                            if (queue.IsEmpty)
                            {
                                _priorities.Remove(highestPriority);
                                _queues.TryRemove(highestPriority, out _);
                            }
                            return true;
                        }
                    }
                    return false;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _queues.Clear();
                _priorities.Clear();
                Interlocked.Exchange(ref _count, 0);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}