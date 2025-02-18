using System;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Event arguments for validator registration events
    /// </summary>
    public class ValidatorRegistrationEventArgs : EventArgs
    {
        /// <summary>
        /// The language identifier associated with the validator
        /// </summary>
        public string LanguageId { get; }

        /// <summary>
        /// The validator instance
        /// </summary>
        public ICodeValidator Validator { get; }

        /// <summary>
        /// Metadata associated with the validator
        /// </summary>
        public ValidatorMetadata Metadata { get; }

        /// <summary>
        /// Timestamp of the registration event
        /// </summary>
        public DateTime Timestamp { get; }

        public ValidatorRegistrationEventArgs(string languageId, ICodeValidator validator, ValidatorMetadata metadata)
        {
            LanguageId = languageId;
            Validator = validator;
            Metadata = metadata;
            Timestamp = DateTime.UtcNow;
        }
    }
}