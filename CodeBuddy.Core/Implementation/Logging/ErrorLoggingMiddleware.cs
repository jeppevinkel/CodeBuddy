using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Logging;

namespace CodeBuddy.Core.Implementation.Logging
{
    public class ErrorLoggingMiddleware
    {
        private readonly ILoggingService _loggingService;
        private readonly ErrorAnalyticsService _errorAnalyticsService;

        public ErrorLoggingMiddleware(ILoggingService loggingService, ErrorAnalyticsService errorAnalyticsService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _errorAnalyticsService = errorAnalyticsService ?? throw new ArgumentNullException(nameof(errorAnalyticsService));
        }

        public async Task LogErrorAsync(Exception exception, string component, string correlationId = null)
        {
            // Log the error
            await _loggingService.ErrorAsync(
                exception.Message,
                exception,
                component,
                correlationId,
                new { StackTrace = exception.StackTrace }
            );

            // Track error analytics
            await _errorAnalyticsService.TrackErrorAsync(exception, component);
        }

        public async Task LogCriticalAsync(Exception exception, string component, string correlationId = null)
        {
            // Log the critical error
            await _loggingService.CriticalAsync(
                exception.Message,
                exception,
                component,
                correlationId,
                new { StackTrace = exception.StackTrace }
            );

            // Track error analytics
            await _errorAnalyticsService.TrackCriticalErrorAsync(exception, component);
        }
    }
}