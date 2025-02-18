using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class CodeValidatorFactory
{
    private readonly ILogger<CodeValidatorFactory> _logger;
    private readonly IValidatorRegistry _registry;
    private readonly ValidationPipeline _pipeline;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, ICodeValidator> _validatorInstances;

    public CodeValidatorFactory(
        ILogger<CodeValidatorFactory> logger,
        IValidatorRegistry registry,
        ValidationPipeline pipeline,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _registry = registry;
        _pipeline = pipeline;
        _serviceProvider = serviceProvider;
        _validatorInstances = new ConcurrentDictionary<string, ICodeValidator>(StringComparer.OrdinalIgnoreCase);
    }

    public ICodeValidator GetValidator(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be null or empty", nameof(language));
        }

        return _validatorInstances.GetOrAdd(language, CreateValidator);
    }

    private ICodeValidator CreateValidator(string language)
    {
        try
        {
            var info = _registry.GetRegisteredValidators()
                .FirstOrDefault(v => v.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

            if (info == null || !info.IsEnabled)
            {
                throw new NotSupportedException($"No enabled validator available for language: {language}");
            }

            var validator = ActivatorUtilities.CreateInstance(_serviceProvider, info.ValidatorType) as ICodeValidator;
            if (validator == null)
            {
                throw new InvalidOperationException($"Failed to create validator instance for language: {language}");
            }

            return new ValidatorProxy(validator, _pipeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating validator for language: {Language}", language);
            throw;
        }
    }

    public bool SupportsLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var info = _registry.GetRegisteredValidators()
            .FirstOrDefault(v => v.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

        return info != null && info.IsEnabled;
    }

    public IReadOnlyCollection<ValidatorInfo> GetAvailableValidators()
    {
        return _registry.GetRegisteredValidators();
    }
}