namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages file system operations with safety checks
/// </summary>
public interface IFileOperations
{
    Task<string> ReadFileAsync(string path);
    Task WriteFileAsync(string path, string content);
    Task<bool> FileExistsAsync(string path);
    Task<IEnumerable<string>> ListFilesAsync(string directory, string searchPattern = "*.*");
}