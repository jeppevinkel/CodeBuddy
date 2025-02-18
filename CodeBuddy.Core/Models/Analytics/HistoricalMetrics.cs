using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Analytics
{
    public class HistoricalMetrics
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public string MiddlewareId { get; set; }
        public MetricType Type { get; set; }

        public enum MetricType
        {
            ExecutionTime,
            SuccessRate,
            RetryCount,
            ResourceUtilization,
            CacheHitRatio,
            Throughput
        }
    }
}