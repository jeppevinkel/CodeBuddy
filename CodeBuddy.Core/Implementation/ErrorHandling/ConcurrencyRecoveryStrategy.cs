using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Recovery strategy for concurrent validation conflicts
    /// </summary>
    public class ConcurrencyRecoveryStrategy : BaseErrorRecoveryStrategy
    {
        private static readonly Random Random = new Random();
        private const string CONFLICT_KEY = "ConflictId";

        public ConcurrencyRecoveryStrategy(RetryPolicy retryPolicy, IErrorAnalyticsService analytics) 
            : base(retryPolicy, analytics)
        {
        }

        public override async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            try
            {
                // Add jitter to prevent thundering herd problem
                var jitter = Random.Next(100, 1000);
                await Task.Delay(jitter);

                if (!context.State.ContainsKey(CONFLICT_KEY))
                {
                    // First attempt - store conflict info
                    context.State[CONFLICT_KEY] = Guid.NewGuid().ToString();
                    return true;
                }

                // Subsequent attempts - check if conflict is resolved
                var conflictId = context.State[CONFLICT_KEY].ToString();
                return await IsConflictResolvedAsync(conflictId);
            }
            catch (Exception ex)
            {
                TrackException(context, ex);
                return false;
            }
        }

        public override bool CanHandle(ValidationError error)
        {
            return error.Category == ErrorCategory.System 
                && error.ErrorCode?.Contains("CONCURRENCY_CONFLICT", StringComparison.OrdinalIgnoreCase) == true;
        }

        private async Task<bool> IsConflictResolvedAsync(string conflictId)
        {
            // Simulate conflict resolution check
            // In real implementation, this would check a distributed lock or resource state
            await Task.Delay(100);
            return true;
        }
    }
}