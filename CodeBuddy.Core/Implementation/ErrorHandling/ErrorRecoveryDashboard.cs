using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    public interface IErrorRecoveryDashboard
    {
        Task<Dictionary<string, double>> GetRecoverySuccessRates();
        Task<List<ErrorPattern>> GetTopErrorPatterns(int limit = 10);
        Task<Dictionary<string, Dictionary<string, double>>> GetResourceUsageMetrics();
        Task<List<CircuitBreakerMetrics>> GetCircuitBreakerStatuses();
        Task<Dictionary<string, List<double>>> GetHistoricalTrends();
    }

    public class ErrorRecoveryDashboard : IErrorRecoveryDashboard
    {
        private readonly IErrorRecoveryAnalytics _analytics;

        public ErrorRecoveryDashboard(IErrorRecoveryAnalytics analytics)
        {
            _analytics = analytics;
        }

        public async Task<Dictionary<string, double>> GetRecoverySuccessRates()
        {
            var successRates = new Dictionary<string, double>();
            var errorTypes = await GetAllErrorTypes();

            foreach (var errorType in errorTypes)
            {
                var metrics = await _analytics.GetErrorMetrics(errorType);
                if (metrics?.AttemptCount > 0)
                {
                    successRates[errorType] = (double)metrics.SuccessCount / metrics.AttemptCount;
                }
            }

            return successRates;
        }

        public async Task<List<ErrorPattern>> GetTopErrorPatterns(int limit = 10)
        {
            var patterns = await _analytics.AnalyzeErrorPatterns();
            return patterns
                .OrderByDescending(p => p.OccurrenceCount)
                .ThenByDescending(p => p.PredictedProbability)
                .Take(limit)
                .ToList();
        }

        public async Task<Dictionary<string, Dictionary<string, double>>> GetResourceUsageMetrics()
        {
            var resourceMetrics = new Dictionary<string, Dictionary<string, double>>();
            var errorTypes = await GetAllErrorTypes();

            foreach (var errorType in errorTypes)
            {
                var metrics = await _analytics.GetErrorMetrics(errorType);
                if (metrics?.ResourceConsumption != null)
                {
                    resourceMetrics[errorType] = metrics.ResourceConsumption;
                }
            }

            return resourceMetrics;
        }

        public async Task<List<CircuitBreakerMetrics>> GetCircuitBreakerStatuses()
        {
            var services = await GetAllServices();
            var statuses = new List<CircuitBreakerMetrics>();

            foreach (var service in services)
            {
                var status = await _analytics.GetCircuitBreakerStatus(service);
                if (status != null)
                {
                    statuses.Add(status);
                }
            }

            return statuses;
        }

        public async Task<Dictionary<string, List<double>>> GetHistoricalTrends()
        {
            var trends = new Dictionary<string, List<double>>();
            var errorTypes = await GetAllErrorTypes();

            foreach (var errorType in errorTypes)
            {
                var metrics = await _analytics.GetErrorMetrics(errorType);
                if (metrics != null)
                {
                    var successRates = CalculateHistoricalSuccessRates(metrics);
                    if (successRates.Any())
                    {
                        trends[errorType] = successRates;
                    }
                }
            }

            return trends;
        }

        private async Task<List<string>> GetAllErrorTypes()
        {
            // This would typically come from a persistent store
            // For now, we'll return a static list
            return new List<string>
            {
                "NetworkTimeout",
                "DatabaseConnection",
                "ValidationError",
                "AuthenticationFailure",
                "ResourceExhaustion"
            };
        }

        private async Task<List<string>> GetAllServices()
        {
            // This would typically come from service discovery
            // For now, we'll return a static list
            return new List<string>
            {
                "UserService",
                "PaymentService",
                "InventoryService",
                "NotificationService"
            };
        }

        private List<double> CalculateHistoricalSuccessRates(ErrorRecoveryMetrics metrics)
        {
            // This would typically calculate success rates over time periods
            // For now, we'll return a simple demonstration
            return new List<double>
            {
                (double)metrics.SuccessCount / metrics.AttemptCount,
                0.85, // Previous period
                0.82, // Two periods ago
                0.78  // Three periods ago
            };
        }
    }
}