using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Default implementation of retry strategy with circuit breaker and exponential backoff
    /// </summary>
    public class DefaultRetryStrategy : IErrorRecoveryStrategy
    {
        private readonly RetryPolicy _policy;
        private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;
        private readonly Random _jitter;

        public DefaultRetryStrategy(RetryPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
            _jitter = new Random();
        }

        public async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var circuitBreaker = GetOrCreateCircuitBreaker(context.Error.ErrorType);
            if (circuitBreaker.IsOpen)
            {
                if (!await ShouldAttemptResetAsync(circuitBreaker))
                    return false;
            }

            try
            {
                if (!_policy.RetryableCategories.Contains(context.Error.Category))
                    return false;

                if (!_policy.MaxRetryAttempts.TryGetValue(context.Error.Category, out int maxAttempts))
                    return false;

                if (context.AttemptCount >= maxAttempts)
                    return false;

                if (context.FirstAttemptTime.HasValue && 
                    (DateTime.UtcNow - context.FirstAttemptTime.Value).TotalMilliseconds > _policy.MaxRetryDurationMs)
                    return false;

                int delayMs = CalculateDelayWithJitter(context.AttemptCount);
                await Task.Delay(delayMs);

                return true;
            }
            catch (Exception)
            {
                circuitBreaker.RecordFailure();
                throw;
            }
        }

        public bool CanHandle(ValidationError error)
        {
            return error != null && _policy.RetryableCategories.Contains(error.Category);
        }

        public Task PrepareNextAttemptAsync(ErrorRecoveryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.AttemptCount++;
            if (!context.FirstAttemptTime.HasValue)
                context.FirstAttemptTime = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        public Task CleanupAsync(ErrorRecoveryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!_policy.PreserveErrorContext)
            {
                context.Reset();
            }

            return Task.CompletedTask;
        }

        private CircuitBreakerState GetOrCreateCircuitBreaker(string errorType)
        {
            return _circuitBreakers.GetOrAdd(errorType, _ => new CircuitBreakerState(_policy.CircuitBreakerThreshold));
        }

        private async Task<bool> ShouldAttemptResetAsync(CircuitBreakerState circuitBreaker)
        {
            if (!circuitBreaker.IsOpen)
                return true;

            if ((DateTime.UtcNow - circuitBreaker.LastFailureTime).TotalMilliseconds >= _policy.CircuitBreakerResetMs)
            {
                circuitBreaker.Reset();
                return true;
            }

            return false;
        }

        private int CalculateDelayWithJitter(int attemptCount)
        {
            double backoffDelay = _policy.BaseDelayMs * Math.Pow(_policy.BackoffFactor, attemptCount);
            double maxJitter = backoffDelay * 0.1; // 10% jitter
            double jitterDelay = backoffDelay + (_jitter.NextDouble() * maxJitter);
            return (int)Math.Min(jitterDelay, _policy.MaxDelayMs);
        }

        private class CircuitBreakerState
        {
            private readonly int _failureThreshold;
            private int _failureCount;
            private readonly object _lock = new object();

            public bool IsOpen { get; private set; }
            public DateTime LastFailureTime { get; private set; }

            public CircuitBreakerState(int failureThreshold)
            {
                _failureThreshold = failureThreshold;
            }

            public void RecordFailure()
            {
                lock (_lock)
                {
                    _failureCount++;
                    LastFailureTime = DateTime.UtcNow;
                    if (_failureCount >= _failureThreshold)
                    {
                        IsOpen = true;
                    }
                }
            }

            public void Reset()
            {
                lock (_lock)
                {
                    _failureCount = 0;
                    IsOpen = false;
                }
            }
        }
    }
}