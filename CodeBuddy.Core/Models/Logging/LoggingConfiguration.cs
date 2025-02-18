using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Logging
{
    public class LoggingConfiguration
    {
        public bool EnableConsoleLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableExternalLogging { get; set; } = false;
        public string LogFilePath { get; set; } = "logs/codebuddy.log";
        public long MaxLogFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        public int MaxLogFileCount { get; set; } = 5;
        public LogSeverity MinimumLogLevel { get; set; } = LogSeverity.Info;
        public Dictionary<string, LogSeverity> ComponentLogLevels { get; set; } = new Dictionary<string, LogSeverity>();
        public bool EnableAsyncLogging { get; set; } = true;
        public int AsyncQueueSize { get; set; } = 1000;
        public double SamplingRate { get; set; } = 1.0; // 1.0 = log everything, 0.1 = log 10% of entries
        public HashSet<string> ExcludedComponents { get; set; } = new HashSet<string>();
    }
}