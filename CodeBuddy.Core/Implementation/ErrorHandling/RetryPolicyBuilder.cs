using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Fluent builder for configuring retry policies
    /// </summary>
    public class RetryPolicyBuilder
    {
        private readonly RetryPolicy _policy = new RetryPolicy();

        /// <summary>
        /// Creates a new retry policy builder with default settings
        /// </summary>
        public static RetryPolicyBuilder Create() => new RetryPolicyBuilder();

        /// <summary>
        /// Sets the maximum number of retry attempts for a specific error category
        /// </summary>
        public RetryPolicyBuilder WithMaxAttempts(ErrorCategory category, int maxAttempts)
        {
            if (maxAttempts < 0)
                throw new ArgumentException("Max attempts must be non-negative", nameof(maxAttempts));

            _policy.MaxRetryAttempts[category] = maxAttempts;
            _policy.RetryableCategories.Add(category);
            return this;
        }

        /// <summary>
        /// Configures exponential backoff settings
        /// </summary>
        public RetryPolicyBuilder WithExponentialBackoff(int baseDelayMs, int maxDelayMs, double backoffFactor = 2.0)
        {
            if (baseDelayMs <= 0)
                throw new ArgumentException("Base delay must be positive", nameof(baseDelayMs));
            if (maxDelayMs < baseDelayMs)
                throw new ArgumentException("Max delay must be greater than or equal to base delay", nameof(maxDelayMs));
            if (backoffFactor <= 1.0)
                throw new ArgumentException("Backoff factor must be greater than 1.0", nameof(backoffFactor));

            _policy.BaseDelayMs = baseDelayMs;
            _policy.MaxDelayMs = maxDelayMs;
            _policy.BackoffFactor = backoffFactor;
            return this;
        }

        /// <summary>
        /// Configures circuit breaker settings
        /// </summary>
        public RetryPolicyBuilder WithCircuitBreaker(int failureThreshold, int resetTimeoutMs)
        {
            if (failureThreshold <= 0)
                throw new ArgumentException("Failure threshold must be positive", nameof(failureThreshold));
            if (resetTimeoutMs <= 0)
                throw new ArgumentException("Reset timeout must be positive", nameof(resetTimeoutMs));

            _policy.CircuitBreakerThreshold = failureThreshold;
            _policy.CircuitBreakerResetMs = resetTimeoutMs;
            return this;
        }

        /// <summary>
        /// Adds a custom recovery strategy for a specific error type
        /// </summary>
        public RetryPolicyBuilder WithRecoveryStrategy<TStrategy>(string errorType) where TStrategy : IErrorRecoveryStrategy
        {
            if (string.IsNullOrEmpty(errorType))
                throw new ArgumentException("Error type cannot be null or empty", nameof(errorType));

            _policy.RecoveryStrategies[errorType] = typeof(TStrategy);
            return this;
        }

        /// <summary>
        /// Sets whether to preserve error context between retry attempts
        /// </summary>
        public RetryPolicyBuilder PreserveErrorContext(bool preserve = true)
        {
            _policy.PreserveErrorContext = preserve;
            return this;
        }

        /// <summary>
        /// Sets the maximum total duration for retry attempts
        /// </summary>
        public RetryPolicyBuilder WithMaxRetryDuration(int maxDurationMs)
        {
            if (maxDurationMs <= 0)
                throw new ArgumentException("Max duration must be positive", nameof(maxDurationMs));

            _policy.MaxRetryDurationMs = maxDurationMs;
            return this;
        }

        /// <summary>
        /// Builds the configured retry policy
        /// </summary>
        public RetryPolicy Build() => _policy;
    }
}