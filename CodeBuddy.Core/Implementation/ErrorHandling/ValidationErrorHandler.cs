using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Handles validation errors using configured retry policies
    /// </summary>
    public class ValidationErrorHandler
    {
        private readonly string _policyName;
        private readonly IErrorRecoveryStrategy _strategy;

        public ValidationErrorHandler(string policyName)
        {
            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(policyName));

            _policyName = policyName;
            _strategy = RetryPolicyFactory.CreateStrategy(policyName);
        }

        /// <summary>
        /// Handles a validation error using the configured retry policy
        /// </summary>
        public async Task<bool> HandleErrorAsync(ValidationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            if (!_strategy.CanHandle(error))
                return false;

            var context = ErrorRecoveryContext.Create(error);
            
            try
            {
                while (await _strategy.AttemptRecoveryAsync(context))
                {
                    await _strategy.PrepareNextAttemptAsync(context);
                    
                    try
                    {
                        // Attempt the operation that failed
                        bool success = await ExecuteOperationAsync(context);
                        if (success)
                            return true;
                    }
                    catch (Exception ex)
                    {
                        context.LastException = ex;
                        continue;
                    }
                }

                return false;
            }
            finally
            {
                await _strategy.CleanupAsync(context);
            }
        }

        private async Task<bool> ExecuteOperationAsync(ErrorRecoveryContext context)
        {
            // This is a placeholder for the actual operation retry logic
            // In a real implementation, this would execute the failed operation again
            
            // For example:
            // if (context.Error.OperationType == "ValidationOperation")
            // {
            //     return await _validationService.RetryValidationAsync(context.Error.Context);
            // }

            await Task.Delay(100); // Simulate some work
            return false;
        }

        /// <summary>
        /// Creates a custom error handler for specific error types
        /// </summary>
        public static ValidationErrorHandler CreateCustomHandler(string policyName, string errorType)
        {
            return new ValidationErrorHandler(policyName, RetryPolicyFactory.CreateCustomStrategy(policyName, errorType));
        }

        private ValidationErrorHandler(string policyName, IErrorRecoveryStrategy strategy)
        {
            _policyName = policyName;
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }
    }
}