using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Errors
{
    /// <summary>
    /// Represents the context for error recovery attempts
    /// </summary>
    public class ErrorRecoveryContext
    {
        /// <summary>
        /// The validation error that triggered recovery
        /// </summary>
        public ValidationError Error { get; set; }

        /// <summary>
        /// Number of recovery attempts made
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Timestamp of the first recovery attempt
        /// </summary>
        public DateTime? FirstAttemptTime { get; set; }

        /// <summary>
        /// Custom state data preserved between recovery attempts
        /// </summary>
        public Dictionary<string, object> State { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Last exception that occurred during recovery
        /// </summary>
        public Exception LastException { get; set; }

        /// <summary>
        /// Resets the recovery context
        /// </summary>
        public void Reset()
        {
            AttemptCount = 0;
            FirstAttemptTime = null;
            LastException = null;
            State.Clear();
        }

        /// <summary>
        /// Creates a new error recovery context
        /// </summary>
        public static ErrorRecoveryContext Create(ValidationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            return new ErrorRecoveryContext
            {
                Error = error,
                AttemptCount = 0,
                FirstAttemptTime = null
            };
        }
    }
}