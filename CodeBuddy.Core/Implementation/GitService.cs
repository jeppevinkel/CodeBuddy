using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Git;
using System.Text;

namespace CodeBuddy.Core.Implementation
{
    public class GitService : IGitService
    {
        private readonly string _workingDirectory;
        private readonly IErrorHandlingService _errorHandler;
        private GitCredentials _credentials;
        private const string GIT_CREDENTIAL_HELPER = "store";

        public GitService(string workingDirectory, IErrorHandlingService errorHandler)
        {
            _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        public async Task<bool> SetCredentialsAsync(GitCredentials credentials)
        {
            try
            {
                _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));

                // Configure git to use credential store
                var (configSuccess, _, configError) = await ExecuteGitCommandAsync($"config credential.helper {GIT_CREDENTIAL_HELPER}");
                if (!configSuccess)
                {
                    await _errorHandler.HandleErrorAsync("Failed to configure git credentials", new { Error = configError });
                    return false;
                }

                // Set up credentials based on the provided type
                if (!string.IsNullOrEmpty(credentials.Token))
                {
                    // Set up token-based authentication
                    var (success, _, error) = await ExecuteGitCommandAsync("config --global credential.helper store");
                    if (success)
                    {
                        string credentialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".git-credentials");
                        await File.WriteAllTextAsync(credentialPath, $"https://{credentials.Token}@github.com\n");
                        return true;
                    }
                    await _errorHandler.HandleErrorAsync("Failed to store token", new { Error = error });
                    return false;
                }
                else if (!string.IsNullOrEmpty(credentials.KeyPath))
                {
                    // Set up SSH key
                    var (success, _, error) = await ExecuteGitCommandAsync($"config core.sshCommand 'ssh -i {credentials.KeyPath}'");
                    if (!success)
                    {
                        await _errorHandler.HandleErrorAsync("Failed to configure SSH key", new { Error = error });
                        return false;
                    }
                    return true;
                }
                else if (!string.IsNullOrEmpty(credentials.Username) && !string.IsNullOrEmpty(credentials.Password))
                {
                    // Store username/password
                    var credentialString = $"protocol=https\nhost=github.com\nusername={credentials.Username}\npassword={credentials.Password}\n";
                    var credentialProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = "credential approve",
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    try
                    {
                        credentialProcess.Start();
                        await credentialProcess.StandardInput.WriteAsync(credentialString);
                        credentialProcess.StandardInput.Close();
                        await credentialProcess.WaitForExitAsync();
                        return credentialProcess.ExitCode == 0;
                    }
                    catch (Exception ex)
                    {
                        await _errorHandler.HandleErrorAsync("Failed to store credentials", new { Error = ex.Message });
                        return false;
                    }
                }

                await _errorHandler.HandleErrorAsync("No valid credentials provided", null);
                return false;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleErrorAsync("Failed to set credentials", new { Error = ex.Message });
                return false;
            }
        }

        public async Task<bool> ValidateCredentialsAsync()
        {
            try
            {
                // Try to access a private resource to validate credentials
                var (success, _, error) = await ExecuteGitCommandAsync("ls-remote");
                if (!success)
                {
                    await _errorHandler.HandleErrorAsync("Failed to validate credentials", new { Error = error });
                }
                return success;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleErrorAsync("Error validating credentials", new { Error = ex.Message });
                return false;
            }
        }

        public async Task<bool> ClearCredentialsAsync()
        {
            try
            {
                // Remove stored credentials
                var tasks = new[]
                {
                    ExecuteGitCommandAsync("config --unset credential.helper"),
                    ExecuteGitCommandAsync("config --unset core.sshCommand")
                };

                var results = await Task.WhenAll(tasks);
                bool success = results.All(r => r.Success);

                if (success)
                {
                    _credentials = null;
                    // Try to remove stored credentials file if it exists
                    string credentialPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".git-credentials");
                    if (File.Exists(credentialPath))
                    {
                        File.Delete(credentialPath);
                    }
                }
                else
                {
                    await _errorHandler.HandleErrorAsync("Failed to clear some credentials", null);
                }

                return success;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleErrorAsync("Error clearing credentials", new { Error = ex.Message });
                return false;
            }
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