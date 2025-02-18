using System;

namespace CodeBuddy.Core.Models.Logging
{
    public class LogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LogSeverity Severity { get; set; }
        public string Message { get; set; }
        public string ComponentName { get; set; }
        public string OperationType { get; set; }
        public string CorrelationId { get; set; }
        public Exception Exception { get; set; }
        public object AdditionalData { get; set; }
    }

    public enum LogSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}