using System.Reflection;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public interface IValidatorRegistry
{
    void RegisterValidator(Type validatorType);
    void RegisterValidator<T>() where T : ICodeValidator;
    void RegisterValidatorsFromAssembly(Assembly assembly);
    void UnregisterValidator(string language);
    bool IsValidatorRegistered(string language);
    IReadOnlyCollection<ValidatorInfo> GetRegisteredValidators();
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