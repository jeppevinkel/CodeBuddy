using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Defines the contract for code validators
    /// </summary>
    public interface ICodeValidator : IDisposable
    {
        /// <summary>
        /// Initializes the validator with any required resources or configurations
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails</exception>
        void Initialize();

        /// <summary>
        /// Validates the provided code according to the specified options
        /// </summary>
        /// <param name="code">The code to validate</param>
        /// <param name="language">The programming language of the code</param>
        /// <param name="options">Validation options and rules</param>
        /// <returns>The validation result</returns>
        Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options);

        /// <summary>
        /// Gets the current state of the validator
        /// </summary>
        ValidatorState State { get; }

        /// <summary>
        /// Event raised when the validator's state changes
        /// </summary>
        event EventHandler<ValidatorStateChangedEventArgs> StateChanged;
    }

    public enum ValidatorState
    {
        Uninitialized,
        Initializing,
        Ready,
        Error,
        Disposing,
        Disposed
    }

    public class ValidatorStateChangedEventArgs : EventArgs
    {
        public ValidatorState OldState { get; }
        public ValidatorState NewState { get; }
        public Exception Error { get; }

        public ValidatorStateChangedEventArgs(ValidatorState oldState, ValidatorState newState, Exception error = null)
        {
            OldState = oldState;
            NewState = newState;
            Error = error;
        }
    }

    public class ValidationOptions
    {
        public bool ValidateSyntax { get; set; } = true;
        public bool ValidateSecurity { get; set; } = true;
        public bool ValidateStyle { get; set; } = true;
        public bool ValidateBestPractices { get; set; } = true;
        public bool ValidateErrorHandling { get; set; } = true;
        public Dictionary<string, object> CustomRules { get; set; } = new();
        public int SecuritySeverityThreshold { get; set; } = 7;
        public string[] ExcludeRules { get; set; } = Array.Empty<string>();
    }
}