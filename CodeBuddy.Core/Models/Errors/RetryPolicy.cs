using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Errors
{
    /// <summary>
    /// Represents the retry policy configuration for error handling
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Maximum number of retry attempts for each error category
        /// </summary>
        public Dictionary<ErrorCategory, int> MaxRetryAttempts { get; set; } = new Dictionary<ErrorCategory, int>();

        /// <summary>
        /// Base delay between retry attempts in milliseconds
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between retry attempts in milliseconds
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Exponential backoff factor for retry delays
        /// </summary>
        public double BackoffFactor { get; set; } = 2.0;

        /// <summary>
        /// Circuit breaker failure threshold before breaking the circuit
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Circuit breaker reset timeout in milliseconds
        /// </summary>
        public int CircuitBreakerResetMs { get; set; } = 60000;

        /// <summary>
        /// Error categories that should be retried
        /// </summary>
        public HashSet<ErrorCategory> RetryableCategories { get; set; } = new HashSet<ErrorCategory>();

        /// <summary>
        /// Custom recovery strategies per error type
        /// </summary>
        public Dictionary<string, Type> RecoveryStrategies { get; set; } = new Dictionary<string, Type>();

        /// <summary>
        /// Flag indicating whether to preserve error context between retries
        /// </summary>
        public bool PreserveErrorContext { get; set; } = true;

        /// <summary>
        /// Maximum total retry duration in milliseconds
        /// </summary>
        public int MaxRetryDurationMs { get; set; } = 300000;
    }
}