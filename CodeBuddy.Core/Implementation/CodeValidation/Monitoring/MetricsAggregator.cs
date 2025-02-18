using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public class MetricsAggregator : IMetricsAggregator
    {
        private readonly ITimeSeriesStorage _timeSeriesStorage;
        private readonly IResourceAnalytics _resourceAnalytics;
        private readonly Dictionary<string, CircuitBreakerState> _circuitBreakerStates;
        private readonly Dictionary<string, Queue<ExecutionRecord>> _executionHistory;
        private readonly Dictionary<string, Queue<RetryRecord>> _retryHistory;

        public MetricsAggregator(
            ITimeSeriesStorage timeSeriesStorage,
            IResourceAnalytics resourceAnalytics)
        {
            _timeSeriesStorage = timeSeriesStorage;
            _resourceAnalytics = resourceAnalytics;
            _circuitBreakerStates = new Dictionary<string, CircuitBreakerState>();
            _executionHistory = new Dictionary<string, Queue<ExecutionRecord>>();
            _retryHistory = new Dictionary<string, Queue<RetryRecord>>();
        }

        public async Task<MiddlewareExecutionMetrics> GetMiddlewareExecutionMetricsAsync(string middlewareName)
        {
            var history = await GetExecutionHistoryAsync(middlewareName);
            var recentExecutions = history.Where(x => x.Timestamp >= DateTime.UtcNow.AddMinutes(-5));

            return new MiddlewareExecutionMetrics
            {
                TotalRequests = recentExecutions.Count(),
                SuccessfulRequests = recentExecutions.Count(x => x.Success),
                FailedRequests = recentExecutions.Count(x => !x.Success),
                AverageExecutionTime = TimeSpan.FromMilliseconds(recentExecutions.Average(x => x.ExecutionTime.TotalMilliseconds)),
                P95ExecutionTime = CalculatePercentile(recentExecutions.Select(x => x.ExecutionTime), 0.95),
                P99ExecutionTime = CalculatePercentile(recentExecutions.Select(x => x.ExecutionTime), 0.99),
                RequestsPerSecond = CalculateRequestsPerSecond(recentExecutions),
                ConcurrentExecutions = recentExecutions.Count(x => x.EndTime == null)
            };
        }

        public async Task<CircuitBreakerMetrics> GetCircuitBreakerMetricsAsync(string middlewareName)
        {
            var state = await GetCircuitBreakerStateAsync(middlewareName);
            var history = await GetCircuitBreakerHistoryAsync(middlewareName);

            return new CircuitBreakerMetrics
            {
                State = state,
                LastStateChange = GetLastStateChange(history),
                TripsLastHour = CountTripsInTimeRange(history, TimeSpan.FromHours(1)),
                TripsLast24Hours = CountTripsInTimeRange(history, TimeSpan.FromHours(24)),
                AverageRecoveryTime = CalculateAverageRecoveryTime(history)
            };
        }

        public async Task<RetryMetrics> GetRetryMetricsAsync(string middlewareName)
        {
            var retries = await GetRetryHistoryAsync(middlewareName);
            var patterns = AnalyzeRetryPatterns(retries);

            return new RetryMetrics
            {
                TotalRetryAttempts = retries.Sum(x => x.AttemptCount),
                SuccessfulRetries = retries.Count(x => x.Successful),
                RetryPatterns = patterns
            };
        }

        public async Task<List<FailureCategory>> GetTopFailureCategoriesAsync(string middlewareName)
        {
            var failures = await GetFailureHistoryAsync(middlewareName);
            return failures
                .GroupBy(x => x.Category)
                .Select(g => new FailureCategory
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Description = g.First().Description,
                    CommonErrors = g.Select(x => x.ErrorMessage)
                                  .GroupBy(x => x)
                                  .OrderByDescending(x => x.Count())
                                  .Take(5)
                                  .Select(x => x.Key)
                                  .ToList()
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();
        }

        public async Task<SystemWideExecutionMetrics> GetSystemWideMetricsAsync()
        {
            var allMetrics = await Task.WhenAll(
                _executionHistory.Keys.Select(GetMiddlewareExecutionMetricsAsync));

            return new SystemWideExecutionMetrics
            {
                SuccessRate = CalculateOverallSuccessRate(allMetrics),
                AverageResponseTime = CalculateOverallAverageResponseTime(allMetrics),
                ActiveValidations = allMetrics.Sum(x => x.ConcurrentExecutions)
            };
        }

        public async Task<Dictionary<string, CircuitBreakerState>> GetCircuitBreakerStatesAsync()
        {
            return _circuitBreakerStates;
        }

        // Helper methods for data storage and retrieval
        private async Task<IEnumerable<ExecutionRecord>> GetExecutionHistoryAsync(string middlewareName)
        {
            if (!_executionHistory.ContainsKey(middlewareName))
            {
                _executionHistory[middlewareName] = new Queue<ExecutionRecord>();
            }
            return _executionHistory[middlewareName];
        }

        private async Task<IEnumerable<RetryRecord>> GetRetryHistoryAsync(string middlewareName)
        {
            if (!_retryHistory.ContainsKey(middlewareName))
            {
                _retryHistory[middlewareName] = new Queue<RetryRecord>();
            }
            return _retryHistory[middlewareName];
        }

        // Helper methods for calculations
        private TimeSpan CalculatePercentile(IEnumerable<TimeSpan> values, double percentile)
        {
            var sortedValues = values.OrderBy(x => x.TotalMilliseconds).ToList();
            var index = (int)Math.Ceiling(percentile * (sortedValues.Count - 1));
            return sortedValues[index];
        }

        private int CalculateRequestsPerSecond(IEnumerable<ExecutionRecord> executions)
        {
            var recentExecutions = executions.Where(x => x.Timestamp >= DateTime.UtcNow.AddSeconds(-1));
            return recentExecutions.Count();
        }

        private double CalculateOverallSuccessRate(IEnumerable<MiddlewareExecutionMetrics> metrics)
        {
            var totalRequests = metrics.Sum(x => x.TotalRequests);
            var successfulRequests = metrics.Sum(x => x.SuccessfulRequests);
            return totalRequests > 0 ? (double)successfulRequests / totalRequests : 0;
        }

        private TimeSpan CalculateOverallAverageResponseTime(IEnumerable<MiddlewareExecutionMetrics> metrics)
        {
            var weightedSum = metrics.Sum(x => x.AverageExecutionTime.TotalMilliseconds * x.TotalRequests);
            var totalRequests = metrics.Sum(x => x.TotalRequests);
            return TimeSpan.FromMilliseconds(totalRequests > 0 ? weightedSum / totalRequests : 0);
        }

        // Additional helper classes
        private class ExecutionRecord
        {
            public DateTime Timestamp { get; set; }
            public DateTime? EndTime { get; set; }
            public TimeSpan ExecutionTime => EndTime.HasValue ? EndTime.Value - Timestamp : TimeSpan.Zero;
            public bool Success { get; set; }
        }

        private class RetryRecord
        {
            public DateTime Timestamp { get; set; }
            public int AttemptCount { get; set; }
            public bool Successful { get; set; }
        }

        private class FailureRecord
        {
            public string Category { get; set; }
            public string Description { get; set; }
            public string ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}