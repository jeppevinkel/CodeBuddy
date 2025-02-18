using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Logging
{
    public class ResourceLoggingConfig
    {
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
        public bool EnableConsoleLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = true;
        public string LogFilePath { get; set; } = "logs/resource-management.log";
        public bool EnableMetricsLogging { get; set; } = true;
        public string MetricsFilePath { get; set; } = "logs/resource-metrics.log";
        public Dictionary<string, LogLevel> ResourceSpecificLogLevels { get; set; } = new Dictionary<string, LogLevel>();
        public int LogRetentionDays { get; set; } = 30;
        public long MaxLogFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string ResourceName { get; set; }
        public string OperationType { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public string Timestamp { get; set; }
        public string CorrelationId { get; set; }
    }
}