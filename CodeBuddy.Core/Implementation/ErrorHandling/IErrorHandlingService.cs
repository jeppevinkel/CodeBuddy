using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Provides comprehensive error handling and analytics capabilities
    /// </summary>
    public interface IErrorHandlingService
    {
        RetryPolicy RetryPolicy { get; set; }
        
        Task HandleErrorAsync(ValidationError error);
        Task HandleErrorsAsync(IEnumerable<ValidationError> errors);
        Task<bool> AttemptRecoveryAsync(ValidationError error);
        Task LogErrorAsync(ValidationError error);
        
        ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category);
        ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category, 
            string filePath, int lineNumber, int columnNumber);
        
        string GetLocalizedErrorMessage(ValidationError error, string cultureName);
        Dictionary<ErrorSeverity, List<ValidationError>> GroupErrorsBySeverity(IEnumerable<ValidationError> errors);
        Dictionary<ErrorCategory, List<ValidationError>> GroupErrorsByCategory(IEnumerable<ValidationError> errors);
        
        void RegisterRecoveryStrategy(IErrorRecoveryStrategy strategy);
        CircuitBreakerStatus GetCircuitBreakerStatus(ErrorCategory category);
        void ResetCircuitBreaker(ErrorCategory category);
        List<RecoveryAttemptResult> GetRecoveryHistory(string errorCode);
    }
}