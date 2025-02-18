using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

internal class ValidatorProxy : ICodeValidator
{
    private readonly ICodeValidator _innerValidator;
    private readonly ValidationPipeline _pipeline;

    public ValidatorProxy(ICodeValidator innerValidator, ValidationPipeline pipeline)
    {
        _innerValidator = innerValidator;
        _pipeline = pipeline;
    }

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
    {
        var context = new ValidationContext(code, language, options, _innerValidator);
        return await _pipeline.ExecuteAsync(context);
    }
}