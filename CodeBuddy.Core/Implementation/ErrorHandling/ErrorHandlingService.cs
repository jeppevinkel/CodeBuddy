using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    public interface IErrorHandlingService
    {
        void RegisterStrategy(string errorType, IErrorRecoveryStrategy strategy);
        Task<bool> HandleError(Exception error);
        Task<ErrorRecoveryContext> RecoverFromError(Exception error);
        Dictionary<string, IErrorRecoveryStrategy> GetRegisteredStrategies();
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IErrorRecoveryStrategy> _strategies;
        private readonly IErrorAnalyticsService _analyticsService;
        private readonly IErrorMonitoringDashboard _dashboard;

        public ErrorHandlingService(
            ILogger logger,
            IErrorAnalyticsService analyticsService,
            IErrorMonitoringDashboard dashboard)
        {
            _logger = logger;
            _analyticsService = analyticsService;
            _dashboard = dashboard;
            _strategies = new Dictionary<string, IErrorRecoveryStrategy>();
        }

        public void RegisterStrategy(string errorType, IErrorRecoveryStrategy strategy)
        {
            if (string.IsNullOrEmpty(errorType))
                throw new ArgumentNullException(nameof(errorType));

            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            _strategies[errorType] = strategy;
        }

        public async Task<bool> HandleError(Exception error)
        {
            var context = await RecoverFromError(error);
            return context.RecoverySuccessful;
        }

        public async Task<ErrorRecoveryContext> RecoverFromError(Exception error)
        {
            _logger.LogError(error, "Error occurred: {ErrorMessage}", error.Message);

            var errorType = error.GetType().Name;
            var context = new ErrorRecoveryContext
            {
                Error = error,
                StartTime = DateTime.UtcNow
            };

            try
            {
                if (_strategies.TryGetValue(errorType, out var strategy))
                {
                    context.Strategy = strategy;
                    context.RecoverySuccessful = await strategy.RecoverAsync(error);
                }
                else
                {
                    _logger.LogWarning("No recovery strategy found for error type: {ErrorType}", errorType);
                    context.RecoverySuccessful = false;
                }
            }
            catch (Exception recoveryError)
            {
                _logger.LogError(recoveryError, "Error during recovery attempt");
                context.RecoverySuccessful = false;
                context.RecoveryError = recoveryError;
            }
            finally
            {
                context.EndTime = DateTime.UtcNow;
                context.RecoveryDuration = context.EndTime - context.StartTime;

                // Record metrics for analytics
                await _analyticsService.RecordError(error, context);
                await _dashboard.UpdateDashboard();
            }

            return context;
        }

        public Dictionary<string, IErrorRecoveryStrategy> GetRegisteredStrategies()
        {
            return new Dictionary<string, IErrorRecoveryStrategy>(_strategies);
        }
    }
}