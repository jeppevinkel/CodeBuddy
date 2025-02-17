using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements safe file system operations
/// </summary>
public class FileOperations : IFileOperations
{
    private readonly ILogger<FileOperations> _logger;

    public FileOperations(ILogger<FileOperations> logger)
    {
        _logger = logger;
    }

    public async Task<string> ReadFileAsync(string path)
    {
        try
        {
            _logger.LogDebug("Reading file from {Path}", path);
            
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not found", path);
            }

            return await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file from {Path}", path);
            throw;
        }
    }

    public async Task WriteFileAsync(string path, string content)
    {
        try
        {
            _logger.LogDebug("Writing file to {Path}", path);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to temporary file first
            var tempPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempPath, content);

            // Move temporary file to target location
            if (File.Exists(path))
            {
                var backupPath = path + ".bak";
                File.Copy(path, backupPath, true);
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file to {Path}", path);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            return await Task.FromResult(File.Exists(path));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence at {Path}", path);
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string directory, string searchPattern = "*.*")
    {
        try
        {
            _logger.LogDebug("Listing files in {Directory} with pattern {Pattern}", directory, searchPattern);
            
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directory}");
            }

            return await Task.FromResult(Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in {Directory}", directory);
            throw;
        }
    }
}