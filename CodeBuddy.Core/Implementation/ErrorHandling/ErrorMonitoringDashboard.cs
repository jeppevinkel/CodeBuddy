using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.Analytics;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    public class PredictiveErrorMetrics
    {
        public List<ErrorPredictionModel> PredictedErrors { get; set; }
        public Dictionary<string, double> RiskFactors { get; set; }
        public List<string> RecommendedActions { get; set; }
    }

    public interface IErrorMonitoringDashboard
    {
        Task<Dictionary<string, int>> GetCurrentErrorStates();
        Task<List<ErrorFrequencyPattern>> GetRecentErrorTrends(TimeSpan window);
        Task<List<RecoveryStrategyMetrics>> GetStrategyPerformance();
        Task<List<string>> GetActiveCircuitBreakers();
        Task UpdateDashboard();
        Task<PredictiveErrorMetrics> GetPredictiveMetrics();
        Task<bool> ExecutePreventiveAction(string errorType, string action);
    }

    public class ErrorMonitoringDashboard : IErrorMonitoringDashboard
    {
        private readonly IErrorAnalyticsService _analyticsService;
        private readonly ITimeSeriesStorage _timeSeriesStorage;
        private readonly IPreemptiveErrorHandler _preemptiveHandler;

        public ErrorMonitoringDashboard(
            IErrorAnalyticsService analyticsService,
            ITimeSeriesStorage timeSeriesStorage,
            IPreemptiveErrorHandler preemptiveHandler)
        {
            _analyticsService = analyticsService;
            _timeSeriesStorage = timeSeriesStorage;
            _preemptiveHandler = preemptiveHandler;
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

            var predictiveMetrics = await GetPredictiveMetrics();

            await _timeSeriesStorage.StoreMetrics("dashboard", new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow },
                { "error_patterns", report.FrequencyPatterns },
                { "strategy_metrics", report.StrategyMetrics },
                { "circuit_breakers", report.CircuitBreakerStats },
                { "recommendations", report.Recommendations },
                { "predicted_errors", predictiveMetrics.PredictedErrors },
                { "risk_factors", predictiveMetrics.RiskFactors }
            });

            // Update prediction model with latest data
            await _preemptiveHandler.UpdatePredictionModel(TimeSpan.FromDays(7));
        }

        public async Task<PredictiveErrorMetrics> GetPredictiveMetrics()
        {
            var predictions = await _preemptiveHandler.PredictPotentialErrors();
            var riskFactors = await _preemptiveHandler.AnalyzeRiskFactors();

            return new PredictiveErrorMetrics
            {
                PredictedErrors = predictions,
                RiskFactors = riskFactors,
                RecommendedActions = predictions
                    .SelectMany(p => p.RecommendedActions)
                    .Distinct()
                    .ToList()
            };
        }

        public async Task<bool> ExecutePreventiveAction(string errorType, string action)
        {
            return await _preemptiveHandler.TriggerPreventiveAction(errorType, action);
        }
    }
}