using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ErrorRecoveryMetrics
    {
        public string ErrorType { get; set; }
        public int AttemptCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AverageRecoveryTime { get; set; }
        public DateTime LastOccurrence { get; set; }
        public Dictionary<string, double> ResourceConsumption { get; set; }
    }

    public class CircuitBreakerMetrics
    {
        public string ServiceName { get; set; }
        public string State { get; set; }
        public int FailureThreshold { get; set; }
        public int CurrentFailureCount { get; set; }
        public DateTime LastStateTransition { get; set; }
        public List<StateTransition> StateTransitions { get; set; }
    }

    public class StateTransition
    {
        public string FromState { get; set; }
        public string ToState { get; set; }
        public DateTime TransitionTime { get; set; }
        public string Reason { get; set; }
    }

    public class RecoveryEfficiencyScore
    {
        public string StrategyName { get; set; }
        public double SuccessRate { get; set; }
        public double ResourceCost { get; set; }
        public double PerformanceImpact { get; set; }
        public double OverallEfficiencyScore { get; set; }
    }

    public class ErrorPattern
    {
        public string PatternId { get; set; }
        public string Description { get; set; }
        public List<string> RelatedErrorTypes { get; set; }
        public int OccurrenceCount { get; set; }
        public double PredictedProbability { get; set; }
        public List<string> SuggestedPreventiveMeasures { get; set; }
    }
}