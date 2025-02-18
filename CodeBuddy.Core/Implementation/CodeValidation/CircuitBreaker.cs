using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    internal class CircuitBreaker
    {
        private readonly ILogger _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private int _failureCount;
        private DateTime _lastFailure;
        private volatile CircuitState _state;

        public CircuitBreaker(ILogger logger, int failureThreshold = 5, TimeSpan? resetTimeout = null)
        {
            _logger = logger;
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
            _state = CircuitState.Closed;
        }

        public CircuitState State => _state;

        public bool IsOpen => _state == CircuitState.Open;

        public bool AllowRequest()
        {
            if (_state == CircuitState.Closed)
            {
                return true;
            }

            if (_state == CircuitState.Open && DateTime.UtcNow - _lastFailure > _resetTimeout)
            {
                _state = CircuitState.HalfOpen;
                _logger.LogInformation("Circuit breaker entering half-open state");
                return true;
            }

            return _state == CircuitState.HalfOpen;
        }

        public void RecordSuccess()
        {
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                Interlocked.Exchange(ref _failureCount, 0);
                _logger.LogInformation("Circuit breaker reset to closed state after successful operation");
            }
        }

        public void RecordFailure()
        {
            _lastFailure = DateTime.UtcNow;

            if (_state == CircuitState.HalfOpen)
            {
                Trip();
                return;
            }

            if (Interlocked.Increment(ref _failureCount) >= _failureThreshold)
            {
                Trip();
            }
        }

        private void Trip()
        {
            _state = CircuitState.Open;
            _logger.LogWarning("Circuit breaker tripped to open state. Failure count: {FailureCount}", _failureCount);
        }
    }

    internal enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}