using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Base implementation of error recovery strategy with common functionality
    /// </summary>
    public abstract class BaseErrorRecoveryStrategy : IErrorRecoveryStrategy
    {
        protected readonly RetryPolicy RetryPolicy;
        protected readonly IErrorAnalyticsService Analytics;

        protected BaseErrorRecoveryStrategy(RetryPolicy retryPolicy, IErrorAnalyticsService analytics)
        {
            RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            Analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public abstract Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context);

        public abstract bool CanHandle(ValidationError error);

        public virtual async Task PrepareNextAttemptAsync(ErrorRecoveryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Record the attempt
            context.AttemptCount++;
            if (!context.FirstAttemptTime.HasValue)
                context.FirstAttemptTime = DateTime.UtcNow;

            // Calculate delay using exponential backoff
            var delayMs = CalculateBackoffDelay(context.AttemptCount);
            await Task.Delay(delayMs);

            // Track metrics
            await Analytics.TrackRecoveryAttemptAsync(context);
        }

        public virtual async Task CleanupAsync(ErrorRecoveryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Cleanup any resources and track final state
            await Analytics.TrackRecoveryCompletionAsync(context);

            if (!RetryPolicy.PreserveErrorContext)
                context.Reset();
        }

        protected virtual int CalculateBackoffDelay(int attemptCount)
        {
            var delay = RetryPolicy.BaseDelayMs * Math.Pow(RetryPolicy.BackoffFactor, attemptCount - 1);
            return (int)Math.Min(delay, RetryPolicy.MaxDelayMs);
        }

        protected virtual bool ShouldContinueRetrying(ErrorRecoveryContext context)
        {
            if (!RetryPolicy.MaxRetryAttempts.TryGetValue(context.Error.Category, out var maxAttempts))
                return false;

            if (context.AttemptCount >= maxAttempts)
                return false;

            var totalDuration = DateTime.UtcNow - context.FirstAttemptTime.GetValueOrDefault();
            if (totalDuration.TotalMilliseconds >= RetryPolicy.MaxRetryDurationMs)
                return false;

            return true;
        }

        protected virtual void TrackException(ErrorRecoveryContext context, Exception ex)
        {
            context.LastException = ex;
            Analytics.TrackExceptionAsync(context, ex).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}