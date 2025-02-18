using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    internal class BackoffManager
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, BackoffInfo> _backoffStates = new();
        private readonly int _maxRetries;
        private readonly TimeSpan _initialBackoff;
        private readonly TimeSpan _maxBackoff;

        public BackoffManager(ILogger logger, int maxRetries = 3)
        {
            _logger = logger;
            _maxRetries = maxRetries;
            _initialBackoff = TimeSpan.FromSeconds(1);
            _maxBackoff = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> ShouldRetryAsync(string operationId, Exception error = null)
        {
            var backoffInfo = _backoffStates.GetOrAdd(operationId, _ => new BackoffInfo());

            if (backoffInfo.RetryCount >= _maxRetries)
            {
                _logger.LogWarning("Maximum retry count reached for operation {OperationId}", operationId);
                _backoffStates.TryRemove(operationId, out _);
                return false;
            }

            if (error != null)
            {
                // Don't retry on certain exceptions
                if (error is OperationCanceledException || error is ObjectDisposedException)
                {
                    return false;
                }
            }

            backoffInfo.RetryCount++;
            var backoffTime = CalculateBackoffTime(backoffInfo.RetryCount);
            
            _logger.LogInformation(
                "Backing off for {BackoffMs}ms before retry {RetryCount} for operation {OperationId}",
                backoffTime.TotalMilliseconds,
                backoffInfo.RetryCount,
                operationId);

            await Task.Delay(backoffTime);
            return true;
        }

        public void Reset(string operationId)
        {
            _backoffStates.TryRemove(operationId, out _);
        }

        private TimeSpan CalculateBackoffTime(int retryCount)
        {
            // Exponential backoff with jitter
            var backoff = _initialBackoff * Math.Pow(2, retryCount - 1);
            var jitter = Random.Shared.NextDouble() * 0.3 + 0.85; // 85-115% of base value
            var finalBackoff = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds * jitter);
            
            return finalBackoff > _maxBackoff ? _maxBackoff : finalBackoff;
        }

        private class BackoffInfo
        {
            public int RetryCount { get; set; }
        }
    }
}