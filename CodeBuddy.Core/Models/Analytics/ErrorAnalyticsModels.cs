using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ErrorFrequencyPattern
    {
        public string ErrorType { get; set; }
        public int Frequency { get; set; }
        public DateTime TimeWindow { get; set; }
        public Dictionary<string, int> CorrelatedErrors { get; set; }
    }

    public class RecoveryStrategyMetrics
    {
        public string StrategyName { get; set; }
        public string ErrorCategory { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageMTTR { get; set; }
        public Dictionary<string, double> ResourceImpact { get; set; }
    }

    public class CircuitBreakerAnalytics
    {
        public string ServiceName { get; set; }
        public int TotalTransitions { get; set; }
        public double CurrentFailureThreshold { get; set; }
        public double RecommendedThreshold { get; set; }
        public List<CircuitBreakerTransition> TransitionHistory { get; set; }
    }

    public class CircuitBreakerTransition
    {
        public DateTime Timestamp { get; set; }
        public string FromState { get; set; }
        public string ToState { get; set; }
        public string Reason { get; set; }
    }

    public class ErrorPatternMetrics
    {
        public string PatternId { get; set; }
        public List<string> ErrorSequence { get; set; }
        public string Environment { get; set; }
        public Dictionary<string, string> ResourceStates { get; set; }
        public double Confidence { get; set; }
        public int OccurrenceCount { get; set; }
    }

    public class ErrorAnalyticsReport
    {
        public DateTime ReportTimestamp { get; set; }
        public List<ErrorFrequencyPattern> FrequencyPatterns { get; set; }
        public List<RecoveryStrategyMetrics> StrategyMetrics { get; set; }
        public List<CircuitBreakerAnalytics> CircuitBreakerStats { get; set; }
        public List<ErrorPatternMetrics> DetectedPatterns { get; set; }
        public Dictionary<string, string> Recommendations { get; set; }
    }
}