using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Recovery strategy for network timeout errors
    /// </summary>
    public class NetworkTimeoutRecoveryStrategy : BaseErrorRecoveryStrategy
    {
        private const string LAST_PING_STATUS = "LastPingStatus";

        public NetworkTimeoutRecoveryStrategy(RetryPolicy retryPolicy, IErrorAnalyticsService analytics) 
            : base(retryPolicy, analytics)
        {
        }

        public override async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            try
            {
                // Check network connectivity
                using var ping = new Ping();
                var result = await ping.SendPingAsync("8.8.8.8", 1000);
                context.State[LAST_PING_STATUS] = result.Status;

                if (result.Status == IPStatus.Success)
                {
                    // Network is back, let validation retry
                    return true;
                }

                return false;
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
                && error.ErrorCode?.StartsWith("NET_TIMEOUT", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}