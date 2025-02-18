using System;
using System.Collections.Generic;
using System.Threading;
using CodeBuddy.Core.Models.Errors;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Factory for creating and managing retry policies
    /// </summary>
    public class RetryPolicyFactory
    {
        private static readonly Dictionary<string, RetryPolicy> _policies = new Dictionary<string, RetryPolicy>();
        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Registers a retry policy with the given name
        /// </summary>
        public static void RegisterPolicy(string name, RetryPolicy policy)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            _lock.EnterWriteLock();
            try
            {
                _policies[name] = policy;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Creates a default retry strategy with the specified policy
        /// </summary>
        public static IErrorRecoveryStrategy CreateStrategy(string policyName)
        {
            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(policyName));

            _lock.EnterReadLock();
            try
            {
                if (!_policies.TryGetValue(policyName, out var policy))
                    throw new KeyNotFoundException($"Retry policy '{policyName}' not found");

                return new DefaultRetryStrategy(policy);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Creates a custom recovery strategy for the specified policy and error type
        /// </summary>
        public static IErrorRecoveryStrategy CreateCustomStrategy(string policyName, string errorType)
        {
            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(policyName));
            if (string.IsNullOrEmpty(errorType))
                throw new ArgumentException("Error type cannot be null or empty", nameof(errorType));

            _lock.EnterReadLock();
            try
            {
                if (!_policies.TryGetValue(policyName, out var policy))
                    throw new KeyNotFoundException($"Retry policy '{policyName}' not found");

                if (!policy.RecoveryStrategies.TryGetValue(errorType, out var strategyType))
                    return new DefaultRetryStrategy(policy);

                return (IErrorRecoveryStrategy)Activator.CreateInstance(strategyType);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes a registered policy
        /// </summary>
        public static void UnregisterPolicy(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));

            _lock.EnterWriteLock();
            try
            {
                _policies.Remove(name);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a policy with the given name exists
        /// </summary>
        public static bool PolicyExists(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            _lock.EnterReadLock();
            try
            {
                return _policies.ContainsKey(name);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}