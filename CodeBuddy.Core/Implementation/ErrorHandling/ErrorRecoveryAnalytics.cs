using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    public interface IErrorRecoveryAnalytics
    {
        Task RecordRecoveryAttempt(string errorType, bool success, TimeSpan duration, Dictionary<string, double> resourceMetrics);
        Task<ErrorRecoveryMetrics> GetErrorMetrics(string errorType);
        Task<List<ErrorPattern>> AnalyzeErrorPatterns();
        Task<RecoveryEfficiencyScore> EvaluateRecoveryStrategy(string strategyName);
        Task UpdateCircuitBreakerMetrics(string service, string newState, string reason);
        Task<CircuitBreakerMetrics> GetCircuitBreakerStatus(string service);
    }

    public class ErrorRecoveryAnalytics : IErrorRecoveryAnalytics
    {
        private readonly Dictionary<string, ErrorRecoveryMetrics> _errorMetrics;
        private readonly Dictionary<string, CircuitBreakerMetrics> _circuitBreakerMetrics;
        private readonly Dictionary<string, List<RecoveryEfficiencyScore>> _strategyScores;

        public ErrorRecoveryAnalytics()
        {
            _errorMetrics = new Dictionary<string, ErrorRecoveryMetrics>();
            _circuitBreakerMetrics = new Dictionary<string, CircuitBreakerMetrics>();
            _strategyScores = new Dictionary<string, List<RecoveryEfficiencyScore>>();
        }

        public async Task RecordRecoveryAttempt(string errorType, bool success, TimeSpan duration, Dictionary<string, double> resourceMetrics)
        {
            if (!_errorMetrics.ContainsKey(errorType))
            {
                _errorMetrics[errorType] = new ErrorRecoveryMetrics
                {
                    ErrorType = errorType,
                    ResourceConsumption = new Dictionary<string, double>()
                };
            }

            var metrics = _errorMetrics[errorType];
            metrics.AttemptCount++;
            if (success) metrics.SuccessCount++;
            else metrics.FailureCount++;

            metrics.AverageRecoveryTime = ((metrics.AverageRecoveryTime * (metrics.AttemptCount - 1)) + duration.TotalMilliseconds) / metrics.AttemptCount;
            metrics.LastOccurrence = DateTime.UtcNow;

            foreach (var resource in resourceMetrics)
            {
                if (!metrics.ResourceConsumption.ContainsKey(resource.Key))
                    metrics.ResourceConsumption[resource.Key] = 0;
                metrics.ResourceConsumption[resource.Key] += resource.Value;
            }
        }

        public async Task<ErrorRecoveryMetrics> GetErrorMetrics(string errorType)
        {
            return _errorMetrics.TryGetValue(errorType, out var metrics) ? metrics : null;
        }

        public async Task<List<ErrorPattern>> AnalyzeErrorPatterns()
        {
            var patterns = new List<ErrorPattern>();
            var errorGroups = _errorMetrics
                .GroupBy(m => new { m.Value.SuccessRate, m.Value.ResourceConsumption.Sum(r => r.Value) })
                .Where(g => g.Count() > 1);

            foreach (var group in errorGroups)
            {
                patterns.Add(new ErrorPattern
                {
                    PatternId = Guid.NewGuid().ToString(),
                    Description = $"Pattern detected: {group.Count()} similar errors with {group.Key.SuccessRate:P} success rate",
                    RelatedErrorTypes = group.Select(e => e.Key).ToList(),
                    OccurrenceCount = group.Sum(e => e.Value.AttemptCount),
                    PredictedProbability = CalculatePredictedProbability(group),
                    SuggestedPreventiveMeasures = GeneratePreventiveMeasures(group)
                });
            }

            return patterns;
        }

        public async Task<RecoveryEfficiencyScore> EvaluateRecoveryStrategy(string strategyName)
        {
            if (!_strategyScores.ContainsKey(strategyName))
                return null;

            var scores = _strategyScores[strategyName];
            var latestScore = scores.LastOrDefault();
            
            if (latestScore == null)
                return null;

            // Calculate trends and update the overall efficiency score
            var trend = CalculateEfficiencyTrend(scores);
            latestScore.OverallEfficiencyScore = (latestScore.SuccessRate * 0.4) +
                                                (1 - latestScore.ResourceCost * 0.3) +
                                                (1 - latestScore.PerformanceImpact * 0.3) +
                                                (trend * 0.1);

            return latestScore;
        }

        public async Task UpdateCircuitBreakerMetrics(string service, string newState, string reason)
        {
            if (!_circuitBreakerMetrics.ContainsKey(service))
            {
                _circuitBreakerMetrics[service] = new CircuitBreakerMetrics
                {
                    ServiceName = service,
                    StateTransitions = new List<StateTransition>(),
                    LastStateTransition = DateTime.UtcNow
                };
            }

            var metrics = _circuitBreakerMetrics[service];
            var oldState = metrics.State;
            metrics.State = newState;
            metrics.LastStateTransition = DateTime.UtcNow;

            metrics.StateTransitions.Add(new StateTransition
            {
                FromState = oldState,
                ToState = newState,
                TransitionTime = DateTime.UtcNow,
                Reason = reason
            });
        }

        public async Task<CircuitBreakerMetrics> GetCircuitBreakerStatus(string service)
        {
            return _circuitBreakerMetrics.TryGetValue(service, out var metrics) ? metrics : null;
        }

        private double CalculatePredictedProbability(IGrouping<dynamic, KeyValuePair<string, ErrorRecoveryMetrics>> group)
        {
            // Implement prediction logic based on historical data
            var totalOccurrences = group.Sum(e => e.Value.AttemptCount);
            var recentOccurrences = group
                .Sum(e => e.Value.LastOccurrence > DateTime.UtcNow.AddHours(-24) ? 1 : 0);

            return (double)recentOccurrences / totalOccurrences;
        }

        private List<string> GeneratePreventiveMeasures(IGrouping<dynamic, KeyValuePair<string, ErrorRecoveryMetrics>> group)
        {
            var measures = new List<string>();
            var avgSuccessRate = group.Average(e => (double)e.Value.SuccessCount / e.Value.AttemptCount);
            var avgRecoveryTime = group.Average(e => e.Value.AverageRecoveryTime);

            if (avgSuccessRate < 0.5)
                measures.Add("Consider implementing more aggressive retry policies");
            if (avgRecoveryTime > 1000)
                measures.Add("Optimize recovery procedures to reduce duration");
            if (group.Any(e => e.Value.ResourceConsumption.Values.Sum() > 1000))
                measures.Add("Implement resource consumption limits during recovery");

            return measures;
        }

        private double CalculateEfficiencyTrend(List<RecoveryEfficiencyScore> scores)
        {
            if (scores.Count < 2)
                return 0;

            var recentScores = scores.TakeLast(5).ToList();
            var trend = 0.0;
            
            for (int i = 1; i < recentScores.Count; i++)
            {
                trend += (recentScores[i].OverallEfficiencyScore - recentScores[i - 1].OverallEfficiencyScore);
            }

            return trend / (recentScores.Count - 1);
        }
    }
}