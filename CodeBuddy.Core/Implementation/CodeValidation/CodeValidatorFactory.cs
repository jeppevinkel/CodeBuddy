using System;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class CodeValidatorFactory
    {
        private readonly ILogger<CodeValidatorFactory> _logger;
        private readonly IValidatorRegistrar _validatorRegistry;

        public CodeValidatorFactory(ILogger<CodeValidatorFactory> logger, IValidatorRegistrar validatorRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validatorRegistry = validatorRegistry ?? throw new ArgumentNullException(nameof(validatorRegistry));

            InitializeDefaultValidators();
        }

        private void InitializeDefaultValidators()
        {
            // Register default validators if they haven't been registered yet
            RegisterValidatorIfNotExists("csharp", new CSharpCodeValidator(_logger), new ValidatorMetadata 
            { 
                Provider = "CodeBuddy",
                Description = "Default C# code validator",
                Version = new Version(1, 0, 0)
            });

            RegisterValidatorIfNotExists("javascript", new JavaScriptCodeValidator(_logger), new ValidatorMetadata 
            { 
                Provider = "CodeBuddy",
                Description = "Default JavaScript code validator",
                Version = new Version(1, 0, 0)
            });

            RegisterValidatorIfNotExists("python", new PythonCodeValidator(_logger), new ValidatorMetadata 
            { 
                Provider = "CodeBuddy",
                Description = "Default Python code validator",
                Version = new Version(1, 0, 0)
            });
        }

        private void RegisterValidatorIfNotExists(string languageId, ICodeValidator validator, ValidatorMetadata metadata)
        {
            if (!_validatorRegistry.HasValidator(languageId))
            {
                _validatorRegistry.RegisterValidator(languageId, validator, metadata);
                _logger.LogInformation($"Registered default validator for {languageId}");
            }
        }

        public ICodeValidator GetValidator(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("Language cannot be null or empty", nameof(language));
            }

            var validator = _validatorRegistry.GetValidator(language);
            if (validator == null)
            {
                throw new NotSupportedException($"No validator available for language: {language}");
            }

            return validator;
        }

        public bool SupportsLanguage(string language)
        {
            return !string.IsNullOrWhiteSpace(language) && _validatorRegistry.HasValidator(language);
        }

        public ValidatorMetadata GetValidatorMetadata(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("Language cannot be null or empty", nameof(language));
            }

            return _validatorRegistry.GetValidatorMetadata(language);
        }
    }
}