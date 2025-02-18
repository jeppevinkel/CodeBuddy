using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public interface IValidationMiddleware
{
    string Name { get; }
    int Order { get; }
    Task<ValidationResult> ProcessAsync(ValidationContext context, ValidationDelegate next);
}

public delegate Task<ValidationResult> ValidationDelegate(ValidationContext context);

public class ValidationContext
{
    public string Code { get; }
    public string Language { get; }
    public ValidationOptions Options { get; }
    public ICodeValidator Validator { get; }
    public IDictionary<string, object> Items { get; }

    public ValidationContext(string code, string language, ValidationOptions options, ICodeValidator validator)
    {
        Code = code;
        Language = language;
        Options = options;
        Validator = validator;
        Items = new Dictionary<string, object>();
    }
}