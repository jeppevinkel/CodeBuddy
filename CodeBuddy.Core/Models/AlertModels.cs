using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public enum AlertSeverity
    {
        Warning,
        Critical,
        Emergency
    }

    public enum ResourceMetricType
    {
        CPU,
        Memory,
        DiskIO
    }

    public class ResourceAlert
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public AlertSeverity Severity { get; set; }
        public ResourceMetricType MetricType { get; set; }
        public string Message { get; set; }
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public string ValidationContext { get; set; }
        public Dictionary<string, object> SystemState { get; set; }
        public Dictionary<string, double> RelatedMetrics { get; set; }

        public ResourceAlert()
        {
            Id = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            SystemState = new Dictionary<string, object>();
            RelatedMetrics = new Dictionary<string, double>();
        }
    }

    public class ResourceThreshold
    {
        public ResourceMetricType MetricType { get; set; }
        public double WarningThreshold { get; set; }
        public double CriticalThreshold { get; set; }
        public double EmergencyThreshold { get; set; }
        public TimeSpan SustainedDuration { get; set; }
        public double RateOfChangeThreshold { get; set; }
    }

    public class AlertConfiguration
    {
        public Dictionary<ResourceMetricType, ResourceThreshold> Thresholds { get; set; }
        public TimeSpan AlertAggregationWindow { get; set; }
        public int MaxAlertsPerWindow { get; set; }
        public bool EnableTrendAnalysis { get; set; }
        public TimeSpan TrendAnalysisWindow { get; set; }

        public AlertConfiguration()
        {
            Thresholds = new Dictionary<ResourceMetricType, ResourceThreshold>();
            AlertAggregationWindow = TimeSpan.FromMinutes(5);
            MaxAlertsPerWindow = 10;
            EnableTrendAnalysis = true;
            TrendAnalysisWindow = TimeSpan.FromHours(1);
        }
    }
}