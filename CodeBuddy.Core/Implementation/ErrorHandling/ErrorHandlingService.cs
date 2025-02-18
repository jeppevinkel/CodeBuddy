using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Default implementation of the error handling service
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly Dictionary<string, Dictionary<string, string>> _localizedMessages;

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger;
            _localizedMessages = new Dictionary<string, Dictionary<string, string>>();
        }

        public async Task HandleErrorAsync(ValidationError error)
        {
            // Log the error
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
    }
}