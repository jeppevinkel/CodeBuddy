using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements safe file system operations
/// </summary>
public class FileOperations : IFileOperations
{
    private readonly ILogger<FileOperations> _logger;

    /// <summary>
    /// Gets the available free space on the drive containing the specified path
    /// </summary>
    private async Task<long> GetAvailableDiskSpaceAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(root))
                {
                    throw new ArgumentException("Unable to determine drive root", nameof(path));
                }

                var driveInfo = new DriveInfo(root);
                return driveInfo.AvailableFreeSpace;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking available disk space for {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Validates that sufficient disk space is available for the requested operation
    /// </summary>
    private async Task ValidateDiskSpaceAsync(string path, long requiredSpace, string operation, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new FileOperationProgress(
            1, 0,
            $"Validating disk space for {operation}"
        ));

        var availableSpace = await GetAvailableDiskSpaceAsync(path, cancellationToken);
        
        if (availableSpace < requiredSpace)
        {
            throw new InsufficientDiskSpaceException(path, requiredSpace, availableSpace);
        }

        progress?.Report(new FileOperationProgress(
            1, 1,
            $"Disk space validated for {operation}"
        ));

        _logger.LogDebug("Disk space validated for {Operation}. Required: {Required}, Available: {Available}", 
            operation, requiredSpace, availableSpace);
    }

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

            // Calculate total space needed (temp file + backup + final file)
            var contentBytes = System.Text.Encoding.UTF8.GetByteCount(content);
            var spaceNeeded = contentBytes * (File.Exists(path) ? 3 : 2); // Temp + Final + Backup (if exists)
            await ValidateDiskSpaceAsync(path, spaceNeeded, $"writing {Path.GetFileName(path)}", progress, cancellationToken);
            
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

    public async Task CreateDirectoryAsync(string path, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Creating directory at {Path}", path);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(path))
                {
                    _logger.LogInformation("Directory already exists at {Path}", path);
                    progress?.Report(new FileOperationProgress(1, 1, $"Directory already exists: {Path.GetFileName(path)}"));
                    return;
                }

                progress?.Report(new FileOperationProgress(1, 0, $"Creating directory: {Path.GetFileName(path)}"));
                Directory.CreateDirectory(path);
                progress?.Report(new FileOperationProgress(1, 1, $"Created directory: {Path.GetFileName(path)}"));
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Directory creation cancelled for {Path}", path);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory at {Path}", path);
            throw;
        }
    }

    public async Task DeleteDirectoryAsync(string path, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Deleting directory at {Path}", path);

            if (!Directory.Exists(path))
            {
                _logger.LogInformation("Directory does not exist at {Path}", path);
                progress?.Report(new FileOperationProgress(1, 1, $"Directory does not exist: {Path.GetFileName(path)}"));
                return;
            }

            // Count total items for progress tracking
            var totalItems = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Length +
                       Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length + 1;
            }, cancellationToken);

            var processed = 0;
            progress?.Report(new FileOperationProgress(totalItems, processed, $"Beginning deletion of {Path.GetFileName(path)}"));

            // Delete files first
            var files = await Task.Run(() => Directory.GetFiles(path, "*.*", SearchOption.AllDirectories), cancellationToken);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => File.Delete(file), cancellationToken);
                processed++;
                progress?.Report(new FileOperationProgress(totalItems, processed, $"Deleted file: {Path.GetFileName(file)}"));
            }

            // Delete subdirectories
            var directories = await Task.Run(() => 
                Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length) // Delete deepest directories first
                    .ToList(), 
                cancellationToken);

            foreach (var dir in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => Directory.Delete(dir, false), cancellationToken);
                processed++;
                progress?.Report(new FileOperationProgress(totalItems, processed, $"Deleted directory: {Path.GetFileName(dir)}"));
            }

            // Delete root directory
            await Task.Run(() => Directory.Delete(path, false), cancellationToken);
            processed++;
            progress?.Report(new FileOperationProgress(totalItems, processed, $"Deleted root directory: {Path.GetFileName(path)}"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Directory deletion cancelled for {Path}", path);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting directory at {Path}", path);
            throw;
        }
    }

    public async Task CopyDirectoryAsync(string sourcePath, string destinationPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Copying directory from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            // Count total items and calculate total bytes for progress tracking
            var (totalItems, totalBytes) = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
                var dirs = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);
                var bytes = files.Sum(f => new FileInfo(f).Length);
                return (files.Length + dirs.Length + 1, bytes);
            }, cancellationToken);

            // Validate disk space
            await ValidateDiskSpaceAsync(destinationPath, totalBytes, $"copying directory {Path.GetFileName(sourcePath)}", progress, cancellationToken);

            var processedItems = 0;
            var processedBytes = 0L;

            // Create destination directory
            if (!Directory.Exists(destinationPath))
            {
                await Task.Run(() => Directory.CreateDirectory(destinationPath), cancellationToken);
                processedItems++;
                progress?.Report(new FileOperationProgress(totalBytes, processedBytes, $"Created directory: {Path.GetFileName(destinationPath)}"));
            }

            // Copy subdirectories
            var directories = await Task.Run(() => Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories), cancellationToken);
            foreach (var dir in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var newDir = dir.Replace(sourcePath, destinationPath);
                await Task.Run(() => Directory.CreateDirectory(newDir), cancellationToken);
                processedItems++;
                progress?.Report(new FileOperationProgress(totalBytes, processedBytes, $"Created directory: {Path.GetFileName(newDir)}"));
            }

            // Copy files
            var files = await Task.Run(() => Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories), cancellationToken);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var newFile = file.Replace(sourcePath, destinationPath);
                
                using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read);
                using var destStream = new FileStream(newFile, FileMode.Create, FileAccess.Write);
                
                var buffer = new byte[81920];
                int read;
                
                while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await destStream.WriteAsync(buffer, 0, read, cancellationToken);
                    processedBytes += read;
                    progress?.Report(new FileOperationProgress(totalBytes, processedBytes, $"Copying file: {Path.GetFileName(file)}"));
                }
                
                processedItems++;
            }

            progress?.Report(new FileOperationProgress(totalBytes, totalBytes, $"Completed copying {Path.GetFileName(sourcePath)}"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Directory copy cancelled from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            if (Directory.Exists(destinationPath))
            {
                try
                {
                    await DeleteDirectoryAsync(destinationPath, null, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error cleaning up destination directory after cancellation");
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying directory from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            if (Directory.Exists(destinationPath))
            {
                try
                {
                    await DeleteDirectoryAsync(destinationPath, null, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error cleaning up destination directory after error");
                }
            }
            throw;
        }
    }

    public async Task MoveDirectoryAsync(string sourcePath, string destinationPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Moving directory from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            if (Directory.Exists(destinationPath))
            {
                throw new IOException($"Destination directory already exists: {destinationPath}");
            }

            // First try an atomic move operation
            try
            {
                await Task.Run(() =>
                {
                    Directory.Move(sourcePath, destinationPath);
                    progress?.Report(new FileOperationProgress(1, 1, $"Moved directory: {Path.GetFileName(sourcePath)}"));
                }, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogInformation(ex, "Atomic move failed, falling back to copy-then-delete");
            }

            // Get total size for space validation before copy-then-delete
            var totalSize = await Task.Run(() =>
            {
                return Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }, cancellationToken);

            // Validate disk space for copy operation
            await ValidateDiskSpaceAsync(destinationPath, totalSize, $"moving directory {Path.GetFileName(sourcePath)}", progress, cancellationToken);

            // If atomic move fails, fall back to copy-then-delete
            progress?.Report(new FileOperationProgress(1, 0, $"Beginning copy of {Path.GetFileName(sourcePath)}"));
            
            // Copy all contents
            await CopyDirectoryAsync(sourcePath, destinationPath, progress, cancellationToken);

            // Verify the copy was successful by comparing file counts and sizes
            var sourceInfo = await Task.Run(() =>
            {
                var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
                return (Count: files.Length, Size: files.Sum(f => new FileInfo(f).Length));
            }, cancellationToken);

            var destInfo = await Task.Run(() =>
            {
                var files = Directory.GetFiles(destinationPath, "*.*", SearchOption.AllDirectories);
                return (Count: files.Length, Size: files.Sum(f => new FileInfo(f).Length));
            }, cancellationToken);

            if (sourceInfo != destInfo)
            {
                throw new IOException("Copy verification failed - size or count mismatch");
            }

            // Delete the source
            progress?.Report(new FileOperationProgress(1, 0, $"Removing source directory: {Path.GetFileName(sourcePath)}"));
            await DeleteDirectoryAsync(sourcePath, null, cancellationToken);
            progress?.Report(new FileOperationProgress(1, 1, $"Completed moving {Path.GetFileName(sourcePath)}"));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Directory move cancelled from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            // Try to cleanup the destination if it was created
            if (Directory.Exists(destinationPath))
            {
                try
                {
                    await DeleteDirectoryAsync(destinationPath, null, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error cleaning up destination directory after cancellation");
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving directory from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            // Try to cleanup the destination if it was created
            if (Directory.Exists(destinationPath))
            {
                try
                {
                    await DeleteDirectoryAsync(destinationPath, null, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error cleaning up destination directory after error");
                }
            }
            throw;
        }
    }
}