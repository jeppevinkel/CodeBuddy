using System.Reflection;

using System.Reflection;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public interface IValidatorRegistry : IDisposable
{
    /// <summary>
    /// Registers a validator type with the registry.
    /// </summary>
    void RegisterValidator(Type validatorType);

    /// <summary>
    /// Registers a validator type with the registry.
    /// </summary>
    void RegisterValidator<T>() where T : ICodeValidator;

    /// <summary>
    /// Registers all validators found in the specified assembly.
    /// </summary>
    void RegisterValidatorsFromAssembly(Assembly assembly);

    /// <summary>
    /// Unregisters a validator for the specified language.
    /// </summary>
    void UnregisterValidator(string language);

    /// <summary>
    /// Checks if a validator is registered for the specified language.
    /// </summary>
    bool IsValidatorRegistered(string language);

    /// <summary>
    /// Gets a read-only collection of all registered validators.
    /// </summary>
    IReadOnlyCollection<ValidatorInfo> GetRegisteredValidators();

    /// <summary>
    /// Gets detailed information about a specific validator.
    /// </summary>
    ValidatorInfo GetValidatorInfo(string language);
}

public class ValidatorInfo
{
    public string Language { get; }
    public Type ValidatorType { get; }
    public IValidatorCapabilities Capabilities { get; }
    public bool IsEnabled { get; set; }

    public ValidatorInfo(string language, Type validatorType, IValidatorCapabilities capabilities, bool isEnabled = true)
    {
        Language = language;
        ValidatorType = validatorType;
        Capabilities = capabilities;
        IsEnabled = isEnabled;
    }
}