using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Errors
{
    /// <summary>
    /// Represents the context for error recovery operations
    /// </summary>
    public class ErrorRecoveryContext
    {
        /// <summary>
        /// The original error that triggered recovery
        /// </summary>
        public ValidationError OriginalError { get; set; }

        /// <summary>
        /// Number of retry attempts made so far
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Timestamp when recovery started
        /// </summary>
        public DateTime RecoveryStartTime { get; set; }

        /// <summary>
        /// History of recovery attempts and their results
        /// </summary>
        public List<RecoveryAttemptResult> RecoveryHistory { get; set; } = new List<RecoveryAttemptResult>();

        /// <summary>
        /// State data preserved between recovery attempts
        /// </summary>
        public Dictionary<string, object> RecoveryState { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Circuit breaker status for this error type
        /// </summary>
        public CircuitBreakerStatus CircuitBreaker { get; set; }

        /// <summary>
        /// Compensation actions to be executed on recovery failure
        /// </summary>
        public List<Action> CompensationActions { get; set; } = new List<Action>();

        /// <summary>
        /// Resources that need cleanup after recovery
        /// </summary>
        public HashSet<IDisposable> ResourcesForCleanup { get; set; } = new HashSet<IDisposable>();

        /// <summary>
        /// Last error encountered during recovery attempts
        /// </summary>
        public Exception LastError { get; set; }

        /// <summary>
        /// Flag indicating whether recovery was successful
        /// </summary>
        public bool IsRecoverySuccessful { get; set; }
    }

    /// <summary>
    /// Represents the result of a single recovery attempt
    /// </summary>
    public class RecoveryAttemptResult
    {
        /// <summary>
        /// Timestamp of the recovery attempt
        /// </summary>
        public DateTime AttemptTime { get; set; }

        /// <summary>
        /// Duration of the recovery attempt in milliseconds
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Whether the recovery attempt was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error encountered during the recovery attempt, if any
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// Additional details about the recovery attempt
        /// </summary>
        public string Details { get; set; }
    }

    /// <summary>
    /// Represents the circuit breaker status
    /// </summary>
    public class CircuitBreakerStatus
    {
        /// <summary>
        /// Current state of the circuit breaker
        /// </summary>
        public CircuitState State { get; set; }

        /// <summary>
        /// Number of consecutive failures
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Last failure timestamp
        /// </summary>
        public DateTime? LastFailureTime { get; set; }

        /// <summary>
        /// When the circuit breaker will attempt to reset
        /// </summary>
        public DateTime? NextResetAttempt { get; set; }
    }

    /// <summary>
    /// Circuit breaker states
    /// </summary>
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}