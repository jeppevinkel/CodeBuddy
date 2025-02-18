using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Default implementation of the error handling service
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private const string DefaultPolicyName = "default";
        private readonly ErrorAnalyticsService _analyticsService;

        public ErrorHandlingService(ErrorAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }

        public async Task<bool> HandleErrorAsync(ValidationError error, string policyName = null)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            policyName ??= DefaultPolicyName;

            try
            {
                var handler = new ValidationErrorHandler(policyName);
                bool result = await handler.HandleErrorAsync(error);

                await _analyticsService.TrackErrorHandlingResultAsync(error, result, policyName);

                return result;
            }
            catch (Exception ex)
            {
                await _analyticsService.TrackErrorHandlingExceptionAsync(error, ex, policyName);
                throw;
            }
        }

        public void RegisterRetryPolicy(string name, RetryPolicy policy)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            RetryPolicyFactory.RegisterPolicy(name, policy);
        }

        public RetryPolicy GetRetryPolicy(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));

            if (!RetryPolicyFactory.PolicyExists(name))
                return null;

            try
            {
                // Create a strategy just to get the policy
                var strategy = RetryPolicyFactory.CreateStrategy(name) as DefaultRetryStrategy;
                return strategy?.GetType().GetField("_policy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(strategy) as RetryPolicy;
            }
            catch
            {
                return null;
            }
        }
    }
}