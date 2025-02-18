using System;

namespace CodeBuddy.Core.Models
{
    public class ValidationCacheConfig
    {
        public int MaxEntries { get; set; } = 10000;
        public int MaxCacheSizeMB { get; set; } = 256;
        public int DefaultTTLMinutes { get; set; } = 60;
        public bool EnableCacheWarming { get; set; } = true;
        
        // Performance monitoring thresholds
        public double MinAcceptableHitRatio { get; set; } = 0.5;
        public long MaxAcceptableLatencyMicroseconds { get; set; } = 1000;
        public int MaxMemoryUtilizationPercent { get; set; } = 90;
        public int MetricsRetentionDays { get; set; } = 30;
        public int MonitoringIntervalSeconds { get; set; } = 60;
        public bool EnablePerformanceAlerts { get; set; } = true;
    }
}