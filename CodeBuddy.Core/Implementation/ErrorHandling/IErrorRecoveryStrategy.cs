using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Interface for implementing error recovery strategies
    /// </summary>
    public interface IErrorRecoveryStrategy
    {
        /// <summary>
        /// Attempts to recover from an error
        /// </summary>
        /// <param name="context">The error recovery context</param>
        /// <returns>True if recovery was successful, false otherwise</returns>
        Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context);

        /// <summary>
        /// Checks if this strategy can handle the given error
        /// </summary>
        /// <param name="error">The error to check</param>
        /// <returns>True if this strategy can handle the error</returns>
        bool CanHandle(ValidationError error);

        /// <summary>
        /// Prepares recovery context for the next attempt
        /// </summary>
        /// <param name="context">The error recovery context to prepare</param>
        Task PrepareNextAttemptAsync(ErrorRecoveryContext context);

        /// <summary>
        /// Performs cleanup after recovery attempts
        /// </summary>
        /// <param name="context">The error recovery context</param>
        Task CleanupAsync(ErrorRecoveryContext context);
    }
}