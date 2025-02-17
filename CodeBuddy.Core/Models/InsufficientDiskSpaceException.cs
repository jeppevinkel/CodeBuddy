using System;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Exception thrown when there is insufficient disk space for a file operation
/// </summary>
public class InsufficientDiskSpaceException : IOException
{
    /// <summary>
    /// Gets the required space in bytes
    /// </summary>
    public long RequiredSpace { get; }

    /// <summary>
    /// Gets the available space in bytes
    /// </summary>
    public long AvailableSpace { get; }

    /// <summary>
    /// Gets the path where space was insufficient
    /// </summary>
    public string Path { get; }

    public InsufficientDiskSpaceException(string path, long requiredSpace, long availableSpace)
        : base($"Insufficient disk space at '{path}'. Required: {FormatBytes(requiredSpace)}, Available: {FormatBytes(availableSpace)}")
    {
        Path = path;
        RequiredSpace = requiredSpace;
        AvailableSpace = availableSpace;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}