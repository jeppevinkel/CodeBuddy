using System;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ResponseTimeMetrics
    {
        public TimeSpan AverageResponseTime { get; set; }
        public TimeSpan P95ResponseTime { get; set; }
        public TimeSpan P99ResponseTime { get; set; }
        public double SlowRequestPercentage { get; set; }
        public int TotalRequests { get; set; }
        public int SlowRequests { get; set; }
    }
}