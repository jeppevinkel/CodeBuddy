using System;

namespace CodeBuddy.Core.Models.Analytics
{
    public class ResponseTimeData
    {
        public string PipelineId { get; set; }
        public double AverageResponseTime { get; set; }
        public double P95ResponseTime { get; set; }
        public double P99ResponseTime { get; set; }
        public double SlowRequestPercentage { get; set; }
        public int TotalRequests { get; set; }
        public int SlowRequests { get; set; }
        public DateTime Timestamp { get; set; }
    }
}