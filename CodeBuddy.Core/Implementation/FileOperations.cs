using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
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

    public async Task<string> ReadFileAsync(string path, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Reading file from {Path}", path);
            
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not found", path);
            }

            var fileInfo = new FileInfo(path);
            var totalBytes = fileInfo.Length;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream);
            var buffer = new char[4096];
            var processed = 0L;
            var content = new System.Text.StringBuilder();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var readCount = await reader.ReadBlockAsync(buffer, 0, buffer.Length, cancellationToken);
                if (readCount == 0) break;
                
                content.Append(buffer, 0, readCount);
                processed += readCount;
                
                progress?.Report(new FileOperationProgress(
                    totalBytes,
                    processed,
                    $"Reading {Path.GetFileName(path)}"
                ));
            }

            return content.ToString();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File read operation cancelled for {Path}", path);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file from {Path}", path);
            throw;
        }
    }

    public async Task WriteFileAsync(string path, string content, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.GetTempFileName();
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
            var totalBytes = System.Text.Encoding.UTF8.GetByteCount(content);
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                var buffer = new char[4096];
                var processed = 0L;
                var remaining = content;

                while (remaining.Length > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var length = Math.Min(buffer.Length, remaining.Length);
                    remaining.CopyTo(0, buffer, 0, length);
                    await writer.WriteAsync(buffer, 0, length);
                    
                    processed += length;
                    remaining = remaining.Substring(length);
                    
                    progress?.Report(new FileOperationProgress(
                        totalBytes,
                        processed,
                        $"Writing {Path.GetFileName(path)}"
                    ));
                }
            }

            // Backup existing file
            if (File.Exists(path))
            {
                var backupPath = path + ".bak";
                progress?.Report(new FileOperationProgress(
                    totalBytes,
                    totalBytes,
                    $"Creating backup of {Path.GetFileName(path)}"
                ));
                File.Copy(path, backupPath, true);
                File.Delete(path);
            }

            // Move temporary file to target location
            progress?.Report(new FileOperationProgress(
                totalBytes,
                totalBytes,
                $"Finalizing {Path.GetFileName(path)}"
            ));
            File.Move(tempPath, path);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File write operation cancelled for {Path}", path);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file to {Path}", path);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => 
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.Exists(path);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File exists check cancelled for {Path}", path);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence at {Path}", path);
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string directory, string searchPattern = "*.*", IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Listing files in {Directory} with pattern {Pattern}", directory, searchPattern);
            
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directory}");
            }

            return await Task.Run(() => 
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Get all matching files
                var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
                var totalFiles = files.Length;
                var processed = 0;

                // Report initial progress
                progress?.Report(new FileOperationProgress(
                    totalFiles,
                    processed,
                    $"Scanning directory {Path.GetFileName(directory)}"
                ));

                var result = new List<string>();
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(file);
                    processed++;

                    progress?.Report(new FileOperationProgress(
                        totalFiles,
                        processed,
                        $"Processing files in {Path.GetFileName(directory)}"
                    ));
                }

                return result;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("List files operation cancelled for {Directory}", directory);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in {Directory}", directory);
            throw;
        }
    }
}