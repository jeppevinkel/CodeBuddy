using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.CLI.Commands
{
    public class GitCommand : Command
    {
        private readonly IFileOperations _fileOps;
        private readonly IGitService _gitService;

        public GitCommand(IFileOperations fileOps, IGitService gitService)
            : base("git", "Git operations helpers")
        {
            _fileOps = fileOps ?? throw new ArgumentNullException(nameof(fileOps));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));

            // Repository initialization
            var initCommand = new Command("init", "Initialize a new Git repository");
            initCommand.SetHandler(HandleInitAsync);
            AddCommand(initCommand);

            // Branch management commands
            var branchCommand = new Command("branch", "Branch management operations");
            var branchNameOption = new Option<string>("--name", "Branch name") { IsRequired = true };
            
            var createBranchCommand = new Command("create", "Create a new branch");
            createBranchCommand.AddOption(branchNameOption);
            createBranchCommand.SetHandler(async (string name) => await HandleCreateBranchAsync(name), branchNameOption);
            
            var deleteBranchCommand = new Command("delete", "Delete a branch");
            deleteBranchCommand.AddOption(branchNameOption);
            deleteBranchCommand.SetHandler(async (string name) => await HandleDeleteBranchAsync(name), branchNameOption);
            
            var switchBranchCommand = new Command("switch", "Switch to a branch");
            switchBranchCommand.AddOption(branchNameOption);
            switchBranchCommand.SetHandler(async (string name) => await HandleSwitchBranchAsync(name), branchNameOption);

            branchCommand.AddCommand(createBranchCommand);
            branchCommand.AddCommand(deleteBranchCommand);
            branchCommand.AddCommand(switchBranchCommand);
            AddCommand(branchCommand);

            // Commit operations
            var stageCommand = new Command("stage", "Stage files for commit");
            var filesOption = new Option<string[]>("--files", "Files to stage") { AllowMultipleArgumentsPerToken = true };
            stageCommand.AddOption(filesOption);
            stageCommand.SetHandler(async (string[] files) => await HandleStageFilesAsync(files), filesOption);
            AddCommand(stageCommand);

            var commitCommand = new Command("commit", "Commit staged changes");
            var messageOption = new Option<string>("--message", "Commit message") { IsRequired = true };
            commitCommand.AddOption(messageOption);
            commitCommand.SetHandler(async (string message) => await HandleCommitAsync(message), messageOption);
            AddCommand(commitCommand);

            var pushCommand = new Command("push", "Push commits to remote");
            var remoteOption = new Option<string>("--remote", () => "origin", "Remote name");
            var remoteBranchOption = new Option<string>("--branch", "Remote branch name");
            pushCommand.AddOption(remoteOption);
            pushCommand.AddOption(remoteBranchOption);
            pushCommand.SetHandler(async (string remote, string branch) => 
                await HandlePushAsync(remote, branch), remoteOption, remoteBranchOption);
            AddCommand(pushCommand);

            // Status and diff commands
            var statusCommand = new Command("status", "Show repository status");
            statusCommand.SetHandler(HandleStatusAsync);
            AddCommand(statusCommand);

            var diffCommand = new Command("diff", "Show file changes");
            var pathOption = new Option<string>("--path", "Path to file or directory");
            diffCommand.AddOption(pathOption);
            diffCommand.SetHandler(async (string path) => await HandleDiffAsync(path), pathOption);
            AddCommand(diffCommand);

            // Pull request commands
            var prCommand = new Command("pr", "Pull request operations");
            var createPrCommand = new Command("create", "Create a pull request");
            var titleOption = new Option<string>("--title", "Pull request title") { IsRequired = true };
            var descriptionOption = new Option<string>("--description", "Pull request description") { IsRequired = true };
            var targetBranchOption = new Option<string>("--target", "Target branch") { IsRequired = true };
            createPrCommand.AddOption(titleOption);
            createPrCommand.AddOption(descriptionOption);
            createPrCommand.AddOption(targetBranchOption);
            createPrCommand.SetHandler(async (string title, string description, string target) =>
                await HandleCreatePullRequestAsync(title, description, target),
                titleOption, descriptionOption, targetBranchOption);
            prCommand.AddCommand(createPrCommand);
            AddCommand(prCommand);

            // Credentials management commands
            var credentialsCommand = new Command("credentials", "Manage Git credentials");
            
            var setCredentialsCommand = new Command("set", "Set Git credentials");
            var usernameOption = new Option<string>("--username", "Git username");
            var passwordOption = new Option<string>("--password", "Git password");
            var tokenOption = new Option<string>("--token", "Git access token");
            var keyPathOption = new Option<string>("--key", "Path to SSH key");
            setCredentialsCommand.AddOption(usernameOption);
            setCredentialsCommand.AddOption(passwordOption);
            setCredentialsCommand.AddOption(tokenOption);
            setCredentialsCommand.AddOption(keyPathOption);
            setCredentialsCommand.SetHandler(async (string username, string password, string token, string keyPath) =>
                await HandleSetCredentialsAsync(username, password, token, keyPath),
                usernameOption, passwordOption, tokenOption, keyPathOption);

            var validateCredentialsCommand = new Command("validate", "Validate current Git credentials");
            validateCredentialsCommand.SetHandler(HandleValidateCredentialsAsync);

            var clearCredentialsCommand = new Command("clear", "Clear stored Git credentials");
            clearCredentialsCommand.SetHandler(HandleClearCredentialsAsync);

            credentialsCommand.AddCommand(setCredentialsCommand);
            credentialsCommand.AddCommand(validateCredentialsCommand);
            credentialsCommand.AddCommand(clearCredentialsCommand);
            AddCommand(credentialsCommand);

            // CodeBuddy specific commands
            var setupCommand = new Command("setup", "Set up CodeBuddy files in repository");
            var projectNameOption = new Option<string>("--name", "Project name");
            var projectTypeOption = new Option<string>("--type", "Project type (e.g., csharp, python, javascript)");
            setupCommand.AddOption(projectNameOption);
            setupCommand.AddOption(projectTypeOption);
            setupCommand.SetHandler(async (string name, string type) => 
                await HandleSetupAsync(name, type), projectNameOption, projectTypeOption);
            AddCommand(setupCommand);
        }

        private async Task HandleInitAsync()
        {
            try
            {
                if (await _gitService.IsGitRepositoryAsync())
                {
                    Console.WriteLine("Directory is already a Git repository.");
                    return;
                }

                if (await _gitService.InitializeRepositoryAsync())
                {
                    Console.WriteLine("Git repository initialized successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to initialize Git repository.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing repository: {ex.Message}");
            }
        }

        private async Task HandleCreateBranchAsync(string branchName)
        {
            try
            {
                if (await _gitService.CreateBranchAsync(branchName))
                {
                    Console.WriteLine($"Branch '{branchName}' created successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to create branch '{branchName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating branch: {ex.Message}");
            }
        }

        private async Task HandleDeleteBranchAsync(string branchName)
        {
            try
            {
                if (await _gitService.DeleteBranchAsync(branchName))
                {
                    Console.WriteLine($"Branch '{branchName}' deleted successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to delete branch '{branchName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting branch: {ex.Message}");
            }
        }

        private async Task HandleSwitchBranchAsync(string branchName)
        {
            try
            {
                if (await _gitService.SwitchBranchAsync(branchName))
                {
                    Console.WriteLine($"Switched to branch '{branchName}'.");
                }
                else
                {
                    Console.WriteLine($"Failed to switch to branch '{branchName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error switching branch: {ex.Message}");
            }
        }

        private async Task HandleStageFilesAsync(string[] files)
        {
            try
            {
                bool success;
                if (files == null || files.Length == 0)
                {
                    success = await _gitService.StageAllAsync();
                    if (success)
                    {
                        Console.WriteLine("All changes staged successfully.");
                    }
                }
                else
                {
                    success = await _gitService.StageFilesAsync(files);
                    if (success)
                    {
                        Console.WriteLine($"Successfully staged {files.Length} file(s).");
                    }
                }

                if (!success)
                {
                    Console.WriteLine("Failed to stage changes.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error staging files: {ex.Message}");
            }
        }

        private async Task HandleCommitAsync(string message)
        {
            try
            {
                if (await _gitService.CommitAsync(message))
                {
                    Console.WriteLine("Changes committed successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to commit changes.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error committing changes: {ex.Message}");
            }
        }

        private async Task HandlePushAsync(string remote, string branch)
        {
            try
            {
                if (await _gitService.PushAsync(remote, branch))
                {
                    Console.WriteLine($"Successfully pushed to {remote}" + 
                        (string.IsNullOrEmpty(branch) ? "" : $"/{branch}"));
                }
                else
                {
                    Console.WriteLine("Failed to push changes.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pushing changes: {ex.Message}");
            }
        }

        private async Task HandleStatusAsync()
        {
            try
            {
                var status = await _gitService.GetStatusAsync();
                if (!string.IsNullOrEmpty(status))
                {
                    Console.WriteLine(status);
                }
                else
                {
                    Console.WriteLine("Failed to get repository status.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status: {ex.Message}");
            }
        }

        private async Task HandleDiffAsync(string path)
        {
            try
            {
                var diff = await _gitService.GetDiffAsync(path);
                if (!string.IsNullOrEmpty(diff))
                {
                    Console.WriteLine(diff);
                }
                else
                {
                    Console.WriteLine("No changes found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting diff: {ex.Message}");
            }
        }

        private async Task HandleCreatePullRequestAsync(string title, string description, string targetBranch)
        {
            try
            {
                if (await _gitService.CreatePullRequestAsync(title, description, targetBranch))
                {
                    Console.WriteLine("Pull request created successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to create pull request.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating pull request: {ex.Message}");
            }
        }

        private async Task HandleSetCredentialsAsync(string username, string password, string token, string keyPath)
        {
            try
            {
                var credentials = new Core.Models.Git.GitCredentials
                {
                    Username = username,
                    Password = password,
                    Token = token,
                    KeyPath = keyPath
                };

                if (await _gitService.SetCredentialsAsync(credentials))
                {
                    Console.WriteLine("Git credentials set successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to set Git credentials.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting credentials: {ex.Message}");
            }
        }

        private async Task HandleValidateCredentialsAsync()
        {
            try
            {
                if (await _gitService.ValidateCredentialsAsync())
                {
                    Console.WriteLine("Git credentials are valid.");
                }
                else
                {
                    Console.WriteLine("Git credentials are invalid or not set.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating credentials: {ex.Message}");
            }
        }

        private async Task HandleClearCredentialsAsync()
        {
            try
            {
                if (await _gitService.ClearCredentialsAsync())
                {
                    Console.WriteLine("Git credentials cleared successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to clear Git credentials.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing credentials: {ex.Message}");
            }
        }

        private async Task HandleSetupAsync(string name, string type)
        {
            try
            {
                Console.WriteLine($"Setting up CodeBuddy for {type} project '{name}'...");
                
                var result = await _fileOps.InitializeProjectAsync(new Core.Models.ProjectStructure
                {
                    Name = name,
                    Type = type
                });

                if (result.Success)
                {
                    await _gitService.StageAllAsync();
                    await _gitService.CommitAsync("Initialize CodeBuddy configuration");
                    Console.WriteLine("CodeBuddy setup completed successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to setup CodeBuddy: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during setup: {ex.Message}");
            }
        }
    }
}