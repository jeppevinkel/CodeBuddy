using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class ValidationMiddlewareMetrics
    {
        public string MiddlewareName { get; set; }
        public SuccessFailureMetrics SuccessMetrics { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public CircuitBreakerMetrics CircuitBreakerMetrics { get; set; }
        public RetryMetrics RetryMetrics { get; set; }
        public ResourceUtilizationMetrics ResourceMetrics { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
    }

    public class SuccessFailureMetrics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
        public List<FailureCategory> TopFailureCategories { get; set; }
    }

    public class PerformanceMetrics
    {
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan P95ExecutionTime { get; set; }
        public TimeSpan P99ExecutionTime { get; set; }
        public int RequestsPerSecond { get; set; }
        public int ConcurrentExecutions { get; set; }
    }

    public class CircuitBreakerMetrics
    {
        public CircuitBreakerState State { get; set; }
        public DateTime? LastStateChange { get; set; }
        public int TripsLastHour { get; set; }
        public int TripsLast24Hours { get; set; }
        public TimeSpan AverageRecoveryTime { get; set; }
    }

    public class RetryMetrics
    {
        public int TotalRetryAttempts { get; set; }
        public int SuccessfulRetries { get; set; }
        public double RetrySuccessRate => TotalRetryAttempts > 0 ? (double)SuccessfulRetries / TotalRetryAttempts : 0;
        public List<RetryPattern> RetryPatterns { get; set; }
    }

    public class ResourceUtilizationMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public int ActiveThreads { get; set; }
        public int PendingTasks { get; set; }
        public double NetworkBandwidthUsageMBps { get; set; }
    }

    public class FailureCategory
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
        public List<string> CommonErrors { get; set; }
    }

    public class RetryPattern
    {
        public int AttemptNumber { get; set; }
        public int Count { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageDelayBetweenRetries { get; set; }
    }

    public class ValidationDashboardSummary
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, ValidationMiddlewareMetrics> MiddlewareMetrics { get; set; }
        public SystemWideMetrics SystemMetrics { get; set; }
        public List<Alert> SystemAlerts { get; set; }
        public HistoricalTrends Trends { get; set; }
    }

    public class SystemWideMetrics
    {
        public double OverallSuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
        public int TotalActiveValidations { get; set; }
        public ResourceUtilizationMetrics TotalResourceUtilization { get; set; }
        public int ActiveCircuitBreakers { get; set; }
        public int TotalAlerts { get; set; }
    }

    public class HistoricalTrends
    {
        public List<TimeSeriesDataPoint> SuccessRates { get; set; }
        public List<TimeSeriesDataPoint> ResponseTimes { get; set; }
        public List<TimeSeriesDataPoint> ResourceUtilization { get; set; }
        public List<TimeSeriesDataPoint> ThroughputRates { get; set; }
    }

    public class TimeSeriesDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Metric { get; set; }
        public string Component { get; set; }
    }
}