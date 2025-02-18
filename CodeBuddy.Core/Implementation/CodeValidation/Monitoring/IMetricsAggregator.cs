using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Monitoring
{
    public interface IMetricsAggregator
    {
        Task<MiddlewareExecutionMetrics> GetMiddlewareExecutionMetricsAsync(string middlewareName);
        Task<CircuitBreakerMetrics> GetCircuitBreakerMetricsAsync(string middlewareName);
        Task<RetryMetrics> GetRetryMetricsAsync(string middlewareName);
        Task<List<FailureCategory>> GetTopFailureCategoriesAsync(string middlewareName);
        Task<SystemWideExecutionMetrics> GetSystemWideMetricsAsync();
        Task<Dictionary<string, CircuitBreakerState>> GetCircuitBreakerStatesAsync();
    }

    public class MiddlewareExecutionMetrics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan P95ExecutionTime { get; set; }
        public TimeSpan P99ExecutionTime { get; set; }
        public int RequestsPerSecond { get; set; }
        public int ConcurrentExecutions { get; set; }
    }

    public class SystemWideExecutionMetrics
    {
        public double SuccessRate { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public int ActiveValidations { get; set; }
    }
}