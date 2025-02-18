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

    // New security-specific options
    public SecurityValidationOptions SecurityOptions { get; set; } = new();
}

public class SecurityValidationOptions
{
    public bool ScanDependencies { get; set; } = true;
    public bool IncludeRuleDescriptions { get; set; } = true;
    public string[] ExcludeVulnerabilityTypes { get; set; } = Array.Empty<string>();
    public SecurityScanLevel ScanLevel { get; set; } = SecurityScanLevel.Standard;
    public Dictionary<string, int> VulnerabilityThresholds { get; set; } = new();
    public bool BlockOnCriticalVulnerabilities { get; set; } = true;
}