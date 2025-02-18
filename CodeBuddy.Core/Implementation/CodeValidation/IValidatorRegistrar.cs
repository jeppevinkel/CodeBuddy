using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Interface for registering and managing code validators
    /// </summary>
    public interface IValidatorRegistrar
    {
        /// <summary>
        /// Registers a new validator for a specific language
        /// </summary>
        /// <param name="languageId">The unique identifier for the language</param>
        /// <param name="validator">The validator instance to register</param>
        /// <param name="metadata">Optional metadata about the validator</param>
        /// <returns>True if registration was successful</returns>
        bool RegisterValidator(string languageId, ICodeValidator validator, ValidatorMetadata metadata = null);

        /// <summary>
        /// Unregisters a validator for a specific language
        /// </summary>
        /// <param name="languageId">The language identifier to unregister</param>
        /// <returns>True if unregistration was successful</returns>
        bool UnregisterValidator(string languageId);

        /// <summary>
        /// Gets a validator for a specific language
        /// </summary>
        /// <param name="languageId">The language identifier</param>
        /// <returns>The registered validator or null if none exists</returns>
        ICodeValidator GetValidator(string languageId);

        /// <summary>
        /// Gets all registered validators
        /// </summary>
        /// <returns>Dictionary of language IDs and their corresponding validators</returns>
        IReadOnlyDictionary<string, ICodeValidator> GetAllValidators();

        /// <summary>
        /// Checks if a validator is registered for a specific language
        /// </summary>
        /// <param name="languageId">The language identifier to check</param>
        /// <returns>True if a validator is registered</returns>
        bool HasValidator(string languageId);

        /// <summary>
        /// Gets metadata for a registered validator
        /// </summary>
        /// <param name="languageId">The language identifier</param>
        /// <returns>The validator metadata or null if not found</returns>
        ValidatorMetadata GetValidatorMetadata(string languageId);

        /// <summary>
        /// Event triggered when a new validator is registered
        /// </summary>
        event EventHandler<ValidatorRegistrationEventArgs> ValidatorRegistered;

        /// <summary>
        /// Event triggered when a validator is unregistered
        /// </summary>
        event EventHandler<ValidatorRegistrationEventArgs> ValidatorUnregistered;
    }
}