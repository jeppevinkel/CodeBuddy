using System.Threading.Tasks;
using CodeBuddy.Core.Models.Git;

namespace CodeBuddy.Core.Interfaces
{
    public interface IGitService
    {
        // Credentials Management
        Task<bool> SetCredentialsAsync(GitCredentials credentials);
        Task<bool> ValidateCredentialsAsync();
        Task<bool> ClearCredentialsAsync();
        // Branch Management
        Task<bool> CreateBranchAsync(string branchName);
        Task<bool> DeleteBranchAsync(string branchName);
        Task<bool> SwitchBranchAsync(string branchName);
        Task<string> GetCurrentBranchAsync();
        
        // Commit Operations
        Task<bool> StageFilesAsync(string[] files);
        Task<bool> StageAllAsync();
        Task<bool> CommitAsync(string message);
        Task<bool> PushAsync(string remote = "origin", string branch = "");
        
        // Status and Diff
        Task<string> GetStatusAsync();
        Task<string> GetDiffAsync(string path = "");
        Task<string[]> GetUnstagedFilesAsync();
        Task<string[]> GetStagedFilesAsync();
        
        // Pull Request
        Task<bool> CreatePullRequestAsync(string title, string description, string targetBranch);
        Task<bool> GetPullRequestStatusAsync(string prNumber);
        
        // Repository Operations
        Task<bool> InitializeRepositoryAsync();
        Task<bool> ValidateRepositoryStateAsync();
        Task<bool> IsGitRepositoryAsync();
    }
}