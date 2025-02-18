namespace CodeBuddy.Core.Models;

/// <summary>
/// Plugin resource usage metrics
/// </summary>
public class ResourceMetrics
{
    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Number of active threads
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Number of open file handles
    /// </summary>
    public int FileHandles { get; set; }

    /// <summary>
    /// Total number of operations processed
    /// </summary>
    public long OperationsProcessed { get; set; }

    /// <summary>
    /// Average operation latency in milliseconds
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Number of errors in the last monitoring interval
    /// </summary>
    public int RecentErrors { get; set; }

    /// <summary>
    /// Current queue size of pending operations
    /// </summary>
    public int PendingOperations { get; set; }
}