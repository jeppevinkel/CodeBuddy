using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class SourceControlProvider : ISourceControlProvider
{
    private readonly ILogger _logger;

    public SourceControlProvider(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<string> GetCurrentCommitAsync()
    {
        try
        {
            return await ExecuteGitCommandAsync("rev-parse HEAD");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get current commit", ex);
            return "unknown";
        }
    }

    public async Task<DateTime> GetCommitTimestampAsync(string commitId)
    {
        try
        {
            var timestamp = await ExecuteGitCommandAsync($"show -s --format=%ct {commitId}");
            if (long.TryParse(timestamp, out var unixTimestamp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get timestamp for commit {commitId}", ex);
        }

        return DateTime.UtcNow;
    }

    private async Task<string> ExecuteGitCommandAsync(string arguments)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        if (!Directory.Exists(Path.Combine(workingDirectory, ".git")))
        {
            throw new InvalidOperationException("Not a git repository");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Git command failed: {error}");
            }

            return output.Trim();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to execute git command: {arguments}", ex);
        }
    }
}