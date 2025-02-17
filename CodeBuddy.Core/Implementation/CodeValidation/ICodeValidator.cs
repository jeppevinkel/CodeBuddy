using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public interface ICodeValidator
{
    Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options);
}

public class ValidationOptions
{
    public bool ValidateSyntax { get; set; } = true;
    public bool ValidateSecurity { get; set; } = true;
    public bool ValidateStyle { get; set; } = true;
    public bool ValidateBestPractices { get; set; } = true;
    public bool ValidateErrorHandling { get; set; } = true;
    public Dictionary<string, object> CustomRules { get; set; } = new();
    public int SecuritySeverityThreshold { get; set; } = 7;
    public string[] ExcludeRules { get; set; } = Array.Empty<string>();
}