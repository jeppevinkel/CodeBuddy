using System;

namespace CodeBuddy.Core.Models;

public class ValidatorHealthInfo
{
    public string Language { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public string Version { get; set; } = string.Empty;
    public Exception? LastError { get; set; }
    public TimeSpan LoadTime { get; set; }
    public long MemoryUsageBytes { get; set; }
}