using System;

namespace CodeBuddy.Core.Models
{
    public class ResourceMetrics
    {
        public DateTime Timestamp { get; set; }
        
        // Memory metrics
        public long TotalMemoryBytes { get; set; }
        public long AvailableMemoryBytes { get; set; }
        public int MemoryPressureLevel { get; set; }
        
        // CPU metrics
        public double CpuUtilizationPercent { get; set; }
        public int ProcessorCount { get; set; }
        
        // Thread metrics
        public int ActiveThreadCount { get; set; }
        public int HandleCount { get; set; }
        
        // Validation metrics
        public int QueueDepth { get; set; }
        public double AverageProcessingTime { get; set; }
        
        // GC metrics
        public long Gen0Collections { get; set; }
        public long Gen1Collections { get; set; }
        public long Gen2Collections { get; set; }
        public TimeSpan TotalGCPauseTime { get; set; }
    }

    public class BottleneckAnalysis
    {
        public ResourceType PrimaryBottleneck { get; set; }
        public double BottleneckSeverity { get; set; }
        public string RecommendedAction { get; set; }
        public Dictionary<ResourceType, double> ResourceUtilization { get; set; }
    }

    public class ResourceCleanupEvent
    {
        public DateTime Timestamp { get; set; }
        public ResourceType ResourceType { get; set; }
        public long ResourcesFreed { get; set; }
        public TimeSpan Duration { get; set; }
        public string Details { get; set; }
    }

    public class MemoryPressureIncident
    {
        public DateTime Timestamp { get; set; }
        public int PressureLevel { get; set; }
        public long AvailableMemoryAtIncident { get; set; }
        public TimeSpan Duration { get; set; }
        public string MitigationAction { get; set; }
    }

    public class ResourcePrediction
    {
        public DateTime PredictionTime { get; set; }
        public Dictionary<ResourceType, double> PredictedUtilization { get; set; }
        public double Confidence { get; set; }
        public List<string> PotentialIssues { get; set; }
    }

    public class ResourceAlertConfig
    {
        public ResourceType ResourceType { get; set; }
        public double ThresholdValue { get; set; }
        public AlertPriority Priority { get; set; }
        public TimeSpan Duration { get; set; }
        public string NotificationEndpoint { get; set; }
    }

    public class ResourceAlert
    {
        public Guid AlertId { get; set; }
        public DateTime Timestamp { get; set; }
        public ResourceType ResourceType { get; set; }
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public AlertPriority Priority { get; set; }
        public string Message { get; set; }
    }

    public enum ResourceType
    {
        Memory,
        CPU,
        Threads,
        Handles,
        ValidationQueue,
        GC
    }

    public enum AlertPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ExportFormat
    {
        JSON,
        CSV,
        XML
    }
}