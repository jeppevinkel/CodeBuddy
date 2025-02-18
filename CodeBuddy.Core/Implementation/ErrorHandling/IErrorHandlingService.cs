using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Interface for the error handling service with retry and recovery capabilities
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Gets or sets the retry policy for error handling
        /// </summary>
        RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Registers an error recovery strategy
        /// </summary>
        void RegisterRecoveryStrategy(IErrorRecoveryStrategy strategy);

        /// <summary>
        /// Attempts to recover from an error using registered strategies
        /// </summary>
        Task<bool> AttemptRecoveryAsync(ValidationError error);

        /// <summary>
        /// Gets the current circuit breaker status for an error category
        /// </summary>
        CircuitBreakerStatus GetCircuitBreakerStatus(ErrorCategory category);

        /// <summary>
        /// Resets the circuit breaker for an error category
        /// </summary>
        void ResetCircuitBreaker(ErrorCategory category);

        /// <summary>
        /// Gets the recovery history for a specific error
        /// </summary>
        List<RecoveryAttemptResult> GetRecoveryHistory(string errorCode);

        /// <summary>
        /// Handles a validation error and performs appropriate actions
        /// </summary>
        Task HandleErrorAsync(ValidationError error);

        /// <summary>
        /// Handles multiple validation errors
        /// </summary>
        Task HandleErrorsAsync(IEnumerable<ValidationError> errors);

        /// <summary>
        /// Creates a new validation error
        /// </summary>
        ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category);

        /// <summary>
        /// Creates a new validation error with code location information
        /// </summary>
        ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category, 
            string filePath, int lineNumber, int columnNumber);

        /// <summary>
        /// Logs an error for tracking and analysis
        /// </summary>
        Task LogErrorAsync(ValidationError error);

        /// <summary>
        /// Gets localized error message
        /// </summary>
        string GetLocalizedErrorMessage(ValidationError error, string cultureName);

        /// <summary>
        /// Groups errors by severity
        /// </summary>
        Dictionary<ErrorSeverity, List<ValidationError>> GroupErrorsBySeverity(IEnumerable<ValidationError> errors);

        /// <summary>
        /// Groups errors by category
        /// </summary>
        Dictionary<ErrorCategory, List<ValidationError>> GroupErrorsByCategory(IEnumerable<ValidationError> errors);
    }
}