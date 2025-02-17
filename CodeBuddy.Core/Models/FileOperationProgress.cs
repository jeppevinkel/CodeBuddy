namespace CodeBuddy.Core.Models;

/// <summary>
/// Represents progress information for file operations
/// </summary>
public class FileOperationProgress
{
    /// <summary>
    /// Gets the total number of bytes to process
    /// </summary>
    public long TotalBytes { get; }

    /// <summary>
    /// Gets the number of bytes processed so far
    /// </summary>
    public long ProcessedBytes { get; }

    /// <summary>
    /// Gets the percentage of completion (0-100)
    /// </summary>
    public int PercentComplete { get; }

    /// <summary>
    /// Gets the current operation description
    /// </summary>
    public string CurrentOperation { get; }

    /// <summary>
    /// Creates a new instance of FileOperationProgress
    /// </summary>
    public FileOperationProgress(long totalBytes, long processedBytes, string currentOperation)
    {
        TotalBytes = totalBytes;
        ProcessedBytes = processedBytes;
        CurrentOperation = currentOperation;
        PercentComplete = TotalBytes > 0 ? (int)((processedBytes * 100) / totalBytes) : 0;
    }
}