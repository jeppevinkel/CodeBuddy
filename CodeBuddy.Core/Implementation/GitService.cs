using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Interfaces;
using System.Text;

namespace CodeBuddy.Core.Implementation
{
    public class GitService : IGitService
    {
        private readonly string _workingDirectory;
        private readonly IErrorHandlingService _errorHandler;

        public GitService(string workingDirectory, IErrorHandlingService errorHandler)
        {
            _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        private async Task<(bool Success, string Output, string Error)> ExecuteGitCommandAsync(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = command,
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) => 
                {
                    if (e.Data != null)
                        outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) => 
                {
                    if (e.Data != null)
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                return (
                    process.ExitCode == 0,
                    outputBuilder.ToString().TrimEnd(),
                    errorBuilder.ToString().TrimEnd()
                );
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleExceptionAsync(ex, "Git command execution failed", new { Command = command });
                return (false, string.Empty, ex.Message);
            }
        }

        public async Task<bool> CreateBranchAsync(string branchName)
        {
            var (success, _, error) = await ExecuteGitCommandAsync($"checkout -b {branchName}");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to create branch", new { BranchName = branchName, Error = error });
            }
            return success;
        }

        public async Task<bool> DeleteBranchAsync(string branchName)
        {
            var (success, _, error) = await ExecuteGitCommandAsync($"branch -d {branchName}");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to delete branch", new { BranchName = branchName, Error = error });
            }
            return success;
        }

        public async Task<bool> SwitchBranchAsync(string branchName)
        {
            var (success, _, error) = await ExecuteGitCommandAsync($"checkout {branchName}");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to switch branch", new { BranchName = branchName, Error = error });
            }
            return success;
        }

        public async Task<string> GetCurrentBranchAsync()
        {
            var (success, output, _) = await ExecuteGitCommandAsync("rev-parse --abbrev-ref HEAD");
            return success ? output : string.Empty;
        }

        public async Task<bool> StageFilesAsync(string[] files)
        {
            if (files == null || !files.Any())
                return false;

            var filesArg = string.Join(" ", files.Select(f => $"\"{f}\""));
            var (success, _, error) = await ExecuteGitCommandAsync($"add {filesArg}");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to stage files", new { Files = files, Error = error });
            }
            return success;
        }

        public async Task<bool> StageAllAsync()
        {
            var (success, _, error) = await ExecuteGitCommandAsync("add .");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to stage all files", new { Error = error });
            }
            return success;
        }

        public async Task<bool> CommitAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var (success, _, error) = await ExecuteGitCommandAsync($"commit -m \"{message}\"");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to commit", new { Message = message, Error = error });
            }
            return success;
        }

        public async Task<bool> PushAsync(string remote = "origin", string branch = "")
        {
            var command = "push " + remote;
            if (!string.IsNullOrEmpty(branch))
                command += $" {branch}";

            var (success, _, error) = await ExecuteGitCommandAsync(command);
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to push", new { Remote = remote, Branch = branch, Error = error });
            }
            return success;
        }

        public async Task<string> GetStatusAsync()
        {
            var (success, output, _) = await ExecuteGitCommandAsync("status");
            return success ? output : string.Empty;
        }

        public async Task<string> GetDiffAsync(string path = "")
        {
            var command = "diff";
            if (!string.IsNullOrEmpty(path))
                command += $" \"{path}\"";

            var (success, output, _) = await ExecuteGitCommandAsync(command);
            return success ? output : string.Empty;
        }

        public async Task<string[]> GetUnstagedFilesAsync()
        {
            var (success, output, _) = await ExecuteGitCommandAsync("ls-files --modified --others --exclude-standard");
            return success ? output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        }

        public async Task<string[]> GetStagedFilesAsync()
        {
            var (success, output, _) = await ExecuteGitCommandAsync("diff --cached --name-only");
            return success ? output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        }

        public async Task<bool> CreatePullRequestAsync(string title, string description, string targetBranch)
        {
            // Note: This is a placeholder. Actual implementation would depend on the Git hosting service (GitHub, GitLab, etc.)
            // and would typically use their respective APIs
            await _errorHandler.HandleErrorAsync("Pull request creation not implemented", 
                new { Title = title, Description = description, TargetBranch = targetBranch });
            return false;
        }

        public async Task<bool> GetPullRequestStatusAsync(string prNumber)
        {
            // Note: This is a placeholder. Actual implementation would depend on the Git hosting service
            await _errorHandler.HandleErrorAsync("Pull request status check not implemented", 
                new { PrNumber = prNumber });
            return false;
        }

        public async Task<bool> InitializeRepositoryAsync()
        {
            var (success, _, error) = await ExecuteGitCommandAsync("init");
            if (!success)
            {
                await _errorHandler.HandleErrorAsync("Failed to initialize repository", new { Error = error });
            }
            return success;
        }

        public async Task<bool> ValidateRepositoryStateAsync()
        {
            // Check if this is a git repository
            if (!await IsGitRepositoryAsync())
                return false;

            // Check if there are any uncommitted changes
            var (success, output, _) = await ExecuteGitCommandAsync("status --porcelain");
            return success && string.IsNullOrWhiteSpace(output);
        }

        public async Task<bool> IsGitRepositoryAsync()
        {
            var (success, _, _) = await ExecuteGitCommandAsync("rev-parse --is-inside-work-tree");
            return success;
        }
    }
}