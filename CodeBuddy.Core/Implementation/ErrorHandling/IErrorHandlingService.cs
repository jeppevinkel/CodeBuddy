using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Interface for error handling service
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Handles a validation error
        /// </summary>
        /// <param name="error">The error to handle</param>
        /// <param name="policyName">Optional name of the retry policy to use</param>
        /// <returns>True if the error was handled successfully</returns>
        Task<bool> HandleErrorAsync(ValidationError error, string policyName = null);

        /// <summary>
        /// Registers a retry policy
        /// </summary>
        /// <param name="name">Name of the policy</param>
        /// <param name="policy">The retry policy to register</param>
        void RegisterRetryPolicy(string name, RetryPolicy policy);

        /// <summary>
        /// Gets a registered retry policy
        /// </summary>
        /// <param name="name">Name of the policy to get</param>
        /// <returns>The retry policy, or null if not found</returns>
        RetryPolicy GetRetryPolicy(string name);
    }
}