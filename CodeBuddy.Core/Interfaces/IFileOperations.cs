using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages file system operations with safety checks
/// </summary>
public interface IFileOperations
{
    /// <summary>
    /// Reads content from a file asynchronously with progress tracking and cancellation support
    /// </summary>
    /// <param name="path">The path to the file</param>
    /// <param name="progress">Optional progress tracking</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The content of the file</returns>
    Task<string> ReadFileAsync(string path, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes content to a file asynchronously with progress tracking and cancellation support
    /// </summary>
    /// <param name="path">The path to the file</param>
    /// <param name="content">The content to write</param>
    /// <param name="progress">Optional progress tracking</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    Task WriteFileAsync(string path, string content, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists asynchronously
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>True if the file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in a directory asynchronously with progress tracking and cancellation support
    /// </summary>
    /// <param name="directory">The directory to search</param>
    /// <param name="searchPattern">The search pattern (default: *.*)</param>
    /// <param name="progress">Optional progress tracking</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Enumerable of file paths</returns>
    Task<IEnumerable<string>> ListFilesAsync(string directory, string searchPattern = "*.*", IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
}