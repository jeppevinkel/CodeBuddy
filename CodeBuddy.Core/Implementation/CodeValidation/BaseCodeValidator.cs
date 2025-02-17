using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public abstract class BaseCodeValidator : ICodeValidator
{
    protected readonly ILogger _logger;

    protected BaseCodeValidator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
    {
        var result = new ValidationResult { Language = language };

        try
        {
            if (options.ValidateSyntax)
            {
                await ValidateSyntaxAsync(code, result);
            }

            if (options.ValidateSecurity)
            {
                await ValidateSecurityAsync(code, result);
            }

            if (options.ValidateStyle)
            {
                await ValidateStyleAsync(code, result);
            }

            if (options.ValidateBestPractices)
            {
                await ValidateBestPracticesAsync(code, result);
            }

            if (options.ValidateErrorHandling)
            {
                await ValidateErrorHandlingAsync(code, result);
            }

            await ValidateCustomRulesAsync(code, result, options.CustomRules);

            // Calculate statistics
            CalculateStatistics(result);

            // Set overall validation status
            result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error 
                || i.Severity == ValidationSeverity.SecurityVulnerability);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code validation for language {Language}", language);
            result.Issues.Add(new ValidationIssue
            {
                Code = "VAL001",
                Message = $"Validation process failed: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            result.IsValid = false;
            return result;
        }
    }

    protected abstract Task ValidateSyntaxAsync(string code, ValidationResult result);
    protected abstract Task ValidateSecurityAsync(string code, ValidationResult result);
    protected abstract Task ValidateStyleAsync(string code, ValidationResult result);
    protected abstract Task ValidateBestPracticesAsync(string code, ValidationResult result);
    protected abstract Task ValidateErrorHandlingAsync(string code, ValidationResult result);
    protected abstract Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules);

    private void CalculateStatistics(ValidationResult result)
    {
        result.Statistics.TotalIssues = result.Issues.Count;
        result.Statistics.SecurityIssues = result.Issues.Count(i => i.Severity == ValidationSeverity.SecurityVulnerability);
        result.Statistics.StyleIssues = result.Issues.Count(i => i.Code.StartsWith("STYLE"));
        result.Statistics.BestPracticeIssues = result.Issues.Count(i => i.Code.StartsWith("BP"));
    }
}