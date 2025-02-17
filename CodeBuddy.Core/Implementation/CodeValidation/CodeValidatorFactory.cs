using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class CodeValidatorFactory
{
    private readonly ILogger<CodeValidatorFactory> _logger;
    private readonly Dictionary<string, ICodeValidator> _validators;

    public CodeValidatorFactory(ILogger<CodeValidatorFactory> logger)
    {
        _logger = logger;
        _validators = new Dictionary<string, ICodeValidator>(StringComparer.OrdinalIgnoreCase)
        {
            { "csharp", new CSharpCodeValidator(logger) },
            { "javascript", new JavaScriptCodeValidator(logger) },
            { "python", new PythonCodeValidator(logger) }
        };
    }

    public ICodeValidator GetValidator(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be null or empty", nameof(language));
        }

        if (_validators.TryGetValue(language, out var validator))
        {
            return validator;
        }

        throw new NotSupportedException($"No validator available for language: {language}");
    }

    public bool SupportsLanguage(string language)
    {
        return !string.IsNullOrWhiteSpace(language) && _validators.ContainsKey(language);
    }
}