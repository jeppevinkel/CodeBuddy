using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Default implementation of the error handling service with retry and recovery capabilities
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly Dictionary<string, Dictionary<string, string>> _localizedMessages;
        private readonly List<IErrorRecoveryStrategy> _recoveryStrategies;
        private readonly Dictionary<ErrorCategory, CircuitBreakerStatus> _circuitBreakers;
        private readonly Dictionary<string, ErrorRecoveryContext> _recoveryContexts;

        public RetryPolicy RetryPolicy { get; set; }

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger;
            _localizedMessages = new Dictionary<string, Dictionary<string, string>>();
            _recoveryStrategies = new List<IErrorRecoveryStrategy>();
            _circuitBreakers = new Dictionary<ErrorCategory, CircuitBreakerStatus>();
            _recoveryContexts = new Dictionary<string, ErrorRecoveryContext>();

            // Initialize default retry policy
            RetryPolicy = new RetryPolicy();
        }

        public async Task HandleErrorAsync(ValidationError error)
        {
            // Create or get recovery context
            var context = GetOrCreateRecoveryContext(error);

            // Check circuit breaker
            var circuitBreaker = GetCircuitBreakerStatus(error.Category);
            if (circuitBreaker.State == CircuitState.Open)
            {
                if (DateTime.UtcNow < circuitBreaker.NextResetAttempt)
                {
                    _logger.LogWarning("Circuit breaker is open for category {category}. Skipping recovery.",
                        error.Category);
                    await LogErrorAsync(error);
                    return;
                }
                circuitBreaker.State = CircuitState.HalfOpen;
            }

            // Try recovery if the error category is retryable
            if (RetryPolicy.RetryableCategories.Contains(error.Category))
            {
                var maxAttempts = RetryPolicy.MaxRetryAttempts.GetValueOrDefault(error.Category, 3);
                var startTime = DateTime.UtcNow;

                while (context.RetryAttempts < maxAttempts)
                {
                    // Check if we've exceeded max retry duration
                    if ((DateTime.UtcNow - startTime).TotalMilliseconds > RetryPolicy.MaxRetryDurationMs)
                    {
                        _logger.LogWarning("Max retry duration exceeded for error {code}", error.ErrorCode);
                        break;
                    }

                    // Attempt recovery
                    if (await AttemptRecoveryAsync(error))
                    {
                        // Recovery successful
                        context.IsRecoverySuccessful = true;
                        if (circuitBreaker.State == CircuitState.HalfOpen)
                        {
                            ResetCircuitBreaker(error.Category);
                        }
                        return;
                    }

                    context.RetryAttempts++;

                    // Calculate delay with exponential backoff
                    var delay = Math.Min(
                        RetryPolicy.BaseDelayMs * Math.Pow(RetryPolicy.BackoffFactor, context.RetryAttempts - 1),
                        RetryPolicy.MaxDelayMs
                    );
                    await Task.Delay((int)delay);
                }

                // Update circuit breaker on failure
                if (!context.IsRecoverySuccessful)
                {
                    circuitBreaker.FailureCount++;
                    circuitBreaker.LastFailureTime = DateTime.UtcNow;
                    if (circuitBreaker.FailureCount >= RetryPolicy.CircuitBreakerThreshold)
                    {
                        circuitBreaker.State = CircuitState.Open;
                        circuitBreaker.NextResetAttempt = DateTime.UtcNow.AddMilliseconds(RetryPolicy.CircuitBreakerResetMs);
                    }
                }
            }

            // Log the error if recovery failed or wasn't attempted
            await LogErrorAsync(error);

            // Additional handling based on severity
            switch (error.Severity)
            {
                case ErrorSeverity.Critical:
                    _logger.LogCritical("Critical error occurred: {message} at {location}", 
                        error.Message, error.GetFormattedLocation());
                    break;
                case ErrorSeverity.Error:
                    _logger.LogError("Error occurred: {message} at {location}", 
                        error.Message, error.GetFormattedLocation());
                    break;
                case ErrorSeverity.Warning:
                    _logger.LogWarning("Warning occurred: {message} at {location}", 
                        error.Message, error.GetFormattedLocation());
                    break;
                case ErrorSeverity.Info:
                    _logger.LogInformation("Info: {message} at {location}", 
                        error.Message, error.GetFormattedLocation());
                    break;
            }
        }

        public async Task HandleErrorsAsync(IEnumerable<ValidationError> errors)
        {
            foreach (var error in errors)
            {
                await HandleErrorAsync(error);
            }
        }

        public ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category)
        {
            return new ValidationError
            {
                Message = message,
                Severity = severity,
                Category = category,
                ErrorCode = GenerateErrorCode(category, severity)
            };
        }

        public ValidationError CreateError(string message, ErrorSeverity severity, ErrorCategory category, 
            string filePath, int lineNumber, int columnNumber)
        {
            var error = CreateError(message, severity, category);
            error.FilePath = filePath;
            error.LineNumber = lineNumber;
            error.ColumnNumber = columnNumber;
            return error;
        }

        public async Task LogErrorAsync(ValidationError error)
        {
            // Log to application insights or other logging system
            var logMessage = $"[{error.ErrorCode}] {error.Severity} - {error.Category}: {error.Message}";
            if (error.HasLocation())
            {
                logMessage += $" at {error.GetFormattedLocation()}";
            }

            _logger.LogError(logMessage);

            // Could add additional async logging operations here
            await Task.CompletedTask;
        }

        public string GetLocalizedErrorMessage(ValidationError error, string cultureName)
        {
            if (string.IsNullOrEmpty(error.LocalizationKey) || 
                !_localizedMessages.ContainsKey(cultureName) || 
                !_localizedMessages[cultureName].ContainsKey(error.LocalizationKey))
            {
                return error.Message;
            }

            return _localizedMessages[cultureName][error.LocalizationKey];
        }

        public Dictionary<ErrorSeverity, List<ValidationError>> GroupErrorsBySeverity(IEnumerable<ValidationError> errors)
        {
            return errors.GroupBy(e => e.Severity)
                        .ToDictionary(g => g.Key, g => g.ToList());
        }

        public Dictionary<ErrorCategory, List<ValidationError>> GroupErrorsByCategory(IEnumerable<ValidationError> errors)
        {
            return errors.GroupBy(e => e.Category)
                        .ToDictionary(g => g.Key, g => g.ToList());
        }

        private string GenerateErrorCode(ErrorCategory category, ErrorSeverity severity)
        {
            return $"CB{category.ToString().Substring(0, 3).ToUpper()}{(int)severity:D2}{DateTime.UtcNow.Ticks % 1000:D3}";
        }

        public void RegisterRecoveryStrategy(IErrorRecoveryStrategy strategy)
        {
            _recoveryStrategies.Add(strategy);
        }

        public async Task<bool> AttemptRecoveryAsync(ValidationError error)
        {
            var context = GetOrCreateRecoveryContext(error);
            var attemptStart = DateTime.UtcNow;

            try
            {
                // Find applicable recovery strategies
                var applicableStrategies = _recoveryStrategies
                    .Where(s => s.CanHandle(error))
                    .ToList();

                if (!applicableStrategies.Any())
                {
                    _logger.LogWarning("No recovery strategy found for error {code}", error.ErrorCode);
                    return false;
                }

                // Try each strategy until one succeeds
                foreach (var strategy in applicableStrategies)
                {
                    await strategy.PrepareNextAttemptAsync(context);
                    
                    if (await strategy.AttemptRecoveryAsync(context))
                    {
                        var result = new RecoveryAttemptResult
                        {
                            AttemptTime = attemptStart,
                            DurationMs = (long)(DateTime.UtcNow - attemptStart).TotalMilliseconds,
                            IsSuccessful = true,
                            Details = $"Recovery successful using strategy {strategy.GetType().Name}"
                        };
                        context.RecoveryHistory.Add(result);
                        return true;
                    }
                }

                // All strategies failed
                var failedResult = new RecoveryAttemptResult
                {
                    AttemptTime = attemptStart,
                    DurationMs = (long)(DateTime.UtcNow - attemptStart).TotalMilliseconds,
                    IsSuccessful = false,
                    Error = context.LastError,
                    Details = "All recovery strategies failed"
                };
                context.RecoveryHistory.Add(failedResult);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recovery attempt for {code}", error.ErrorCode);
                context.LastError = ex;
                var errorResult = new RecoveryAttemptResult
                {
                    AttemptTime = attemptStart,
                    DurationMs = (long)(DateTime.UtcNow - attemptStart).TotalMilliseconds,
                    IsSuccessful = false,
                    Error = ex,
                    Details = "Exception during recovery attempt"
                };
                context.RecoveryHistory.Add(errorResult);
                return false;
            }
        }

        public CircuitBreakerStatus GetCircuitBreakerStatus(ErrorCategory category)
        {
            if (!_circuitBreakers.ContainsKey(category))
            {
                _circuitBreakers[category] = new CircuitBreakerStatus
                {
                    State = CircuitState.Closed,
                    FailureCount = 0
                };
            }
            return _circuitBreakers[category];
        }

        public void ResetCircuitBreaker(ErrorCategory category)
        {
            if (_circuitBreakers.ContainsKey(category))
            {
                _circuitBreakers[category] = new CircuitBreakerStatus
                {
                    State = CircuitState.Closed,
                    FailureCount = 0
                };
            }
        }

        public List<RecoveryAttemptResult> GetRecoveryHistory(string errorCode)
        {
            if (_recoveryContexts.TryGetValue(errorCode, out var context))
            {
                return context.RecoveryHistory;
            }
            return new List<RecoveryAttemptResult>();
        }

        private ErrorRecoveryContext GetOrCreateRecoveryContext(ValidationError error)
        {
            if (!_recoveryContexts.TryGetValue(error.ErrorCode, out var context))
            {
                context = new ErrorRecoveryContext
                {
                    OriginalError = error,
                    RecoveryStartTime = DateTime.UtcNow,
                    RetryAttempts = 0,
                    CircuitBreaker = GetCircuitBreakerStatus(error.Category)
                };
                _recoveryContexts[error.ErrorCode] = context;
            }
            return context;
        }
    }
}