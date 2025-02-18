using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Circuit breaker state
    /// </summary>
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Implementation of the Circuit Breaker pattern
    /// </summary>
    public class CircuitBreaker
    {
        private readonly object _lock = new object();
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private volatile CircuitState _state;
        private int _failureCount;
        private DateTime? _lastFailureTime;
        private readonly ConcurrentDictionary<string, CircuitBreakerMetrics> _metrics;

        public CircuitBreaker(int failureThreshold, TimeSpan resetTimeout)
        {
            if (failureThreshold <= 0)
                throw new ArgumentException("Failure threshold must be positive", nameof(failureThreshold));

            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout;
            _state = CircuitState.Closed;
            _metrics = new ConcurrentDictionary<string, CircuitBreakerMetrics>();
        }

        public CircuitState State => _state;

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> action,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            await CheckStateAsync();

            if (_state == CircuitState.Open)
                throw new CircuitBreakerOpenException("Circuit breaker is open");

            try
            {
                var result = await action();
                OnSuccess(operationKey);
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(ex, operationKey);
                throw;
            }
        }

        private async Task CheckStateAsync()
        {
            if (_state == CircuitState.Open && ShouldAttemptReset())
            {
                lock (_lock)
                {
                    if (_state == CircuitState.Open)
                        _state = CircuitState.HalfOpen;
                }
            }

            if (_state == CircuitState.HalfOpen)
            {
                // Only allow one request through in half-open state
                await Task.Delay(100); // Small delay to prevent thundering herd
            }
        }

        private bool ShouldAttemptReset()
        {
            return _lastFailureTime.HasValue && 
                   DateTime.UtcNow - _lastFailureTime.Value >= _resetTimeout;
        }

        private void OnSuccess(string operationKey)
        {
            if (_state == CircuitState.HalfOpen)
            {
                lock (_lock)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _lastFailureTime = null;
                }
            }

            var metrics = _metrics.GetOrAdd(operationKey, _ => new CircuitBreakerMetrics());
            metrics.RecordSuccess();
        }

        private void OnFailure(Exception ex, string operationKey)
        {
            var metrics = _metrics.GetOrAdd(operationKey, _ => new CircuitBreakerMetrics());
            metrics.RecordFailure(ex);

            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen || _failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                }
            }
        }

        public CircuitBreakerMetrics GetMetrics(string operationKey)
        {
            return _metrics.GetOrAdd(operationKey, _ => new CircuitBreakerMetrics());
        }
    }

    /// <summary>
    /// Metrics for circuit breaker operations
    /// </summary>
    public class CircuitBreakerMetrics
    {
        private long _totalRequests;
        private long _successfulRequests;
        private long _failedRequests;
        private DateTime? _lastFailureTime;
        private string _lastFailureReason;

        public void RecordSuccess()
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _successfulRequests);
        }

        public void RecordFailure(Exception ex)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _failedRequests);
            _lastFailureTime = DateTime.UtcNow;
            _lastFailureReason = ex.Message;
        }

        public (long total, long success, long failure) GetRequestCounts()
        {
            return (_totalRequests, _successfulRequests, _failedRequests);
        }

        public (DateTime? time, string reason) GetLastFailure()
        {
            return (_lastFailureTime, _lastFailureReason);
        }
    }

    /// <summary>
    /// Exception thrown when circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }
    }
}