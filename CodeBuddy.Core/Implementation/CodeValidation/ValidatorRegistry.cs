using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidatorRegistry : IValidatorRegistry
{
    private readonly ILogger<ValidatorRegistry> _logger;
    private readonly Dictionary<string, ValidatorInfo> _validators;
    private readonly object _lock = new();

    public ValidatorRegistry(ILogger<ValidatorRegistry> logger)
    {
        _logger = logger;
        _validators = new Dictionary<string, ValidatorInfo>(StringComparer.OrdinalIgnoreCase);
    }

    public void RegisterValidator(Type validatorType)
    {
        if (!typeof(ICodeValidator).IsAssignableFrom(validatorType))
        {
            throw new ArgumentException($"Type {validatorType.FullName} does not implement ICodeValidator");
        }

        if (!typeof(IValidatorCapabilities).IsAssignableFrom(validatorType))
        {
            throw new ArgumentException($"Type {validatorType.FullName} does not implement IValidatorCapabilities");
        }

        var instance = Activator.CreateInstance(validatorType) as IValidatorCapabilities;
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of {validatorType.FullName}");
        }

        lock (_lock)
        {
            var validatorInfo = new ValidatorInfo(instance.Language, validatorType, instance);
            _validators[instance.Language] = validatorInfo;
            _logger.LogInformation("Registered validator for language: {Language}", instance.Language);
        }
    }

    public void RegisterValidator<T>() where T : ICodeValidator
    {
        RegisterValidator(typeof(T));
    }

    public void RegisterValidatorsFromAssembly(Assembly assembly)
    {
        var validatorTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(ICodeValidator).IsAssignableFrom(t)
                && typeof(IValidatorCapabilities).IsAssignableFrom(t));

        foreach (var type in validatorTypes)
        {
            try
            {
                RegisterValidator(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register validator type: {Type}", type.FullName);
            }
        }
    }

    public void UnregisterValidator(string language)
    {
        lock (_lock)
        {
            if (_validators.Remove(language))
            {
                _logger.LogInformation("Unregistered validator for language: {Language}", language);
            }
        }
    }

    public bool IsValidatorRegistered(string language)
    {
        return _validators.ContainsKey(language);
    }

    public IReadOnlyCollection<ValidatorInfo> GetRegisteredValidators()
    {
        lock (_lock)
        {
            return _validators.Values.ToList().AsReadOnly();
        }
    }

    internal ValidatorInfo GetValidatorInfo(string language)
    {
        if (_validators.TryGetValue(language, out var info))
        {
            return info;
        }

        throw new KeyNotFoundException($"No validator registered for language: {language}");
    }
}