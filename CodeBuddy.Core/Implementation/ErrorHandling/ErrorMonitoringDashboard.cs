using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    public interface IErrorMonitoringDashboard
    {
        Task<Dictionary<string, int>> GetCurrentErrorStates();
        Task<List<ErrorFrequencyPattern>> GetRecentErrorTrends(TimeSpan window);
        Task<List<RecoveryStrategyMetrics>> GetStrategyPerformance();
        Task<List<string>> GetActiveCircuitBreakers();
        Task UpdateDashboard();
    }

    public class ErrorMonitoringDashboard : IErrorMonitoringDashboard
    {
        private readonly IErrorAnalyticsService _analyticsService;
        private readonly ITimeSeriesStorage _timeSeriesStorage;

        public ErrorMonitoringDashboard(
            IErrorAnalyticsService analyticsService,
            ITimeSeriesStorage timeSeriesStorage)
        {
            _analyticsService = analyticsService;
            _timeSeriesStorage = timeSeriesStorage;
        }

        public async Task<Dictionary<string, int>> GetCurrentErrorStates()
        {
            var currentTime = DateTime.UtcNow;
            var lastHour = currentTime.AddHours(-1);
            var errorMetrics = await _timeSeriesStorage.GetMetrics("errors", lastHour, currentTime);

            return errorMetrics
                .GroupBy(e => e["type"].ToString())
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );
        }

        public async Task<List<ErrorFrequencyPattern>> GetRecentErrorTrends(TimeSpan window)
        {
            var startTime = DateTime.UtcNow - window;
            return await _analyticsService.AnalyzeErrorPatterns(startTime);
        }

        public async Task<List<RecoveryStrategyMetrics>> GetStrategyPerformance()
        {
            return await _analyticsService.EvaluateRecoveryStrategies();
        }

        public async Task<List<string>> GetActiveCircuitBreakers()
        {
            var circuitBreakers = await _analyticsService.AnalyzeCircuitBreakerPatterns();
            return circuitBreakers
                .Where(cb => cb.CurrentFailureThreshold > cb.RecommendedThreshold)
                .Select(cb => cb.ServiceName)
                .ToList();
        }

        public async Task UpdateDashboard()
        {
            var report = await _analyticsService.GenerateAnalyticsReport(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow);

            await _timeSeriesStorage.StoreMetrics("dashboard", new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow },
                { "error_patterns", report.FrequencyPatterns },
                { "strategy_metrics", report.StrategyMetrics },
                { "circuit_breakers", report.CircuitBreakerStats },
                { "recommendations", report.Recommendations }
            });
        }
    }
}