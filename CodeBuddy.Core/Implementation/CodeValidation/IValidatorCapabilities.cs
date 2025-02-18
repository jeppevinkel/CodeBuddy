using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public interface IValidatorCapabilities
{
    string Language { get; }
    Version Version { get; }
    bool SupportsFeature(ValidationFeature feature);
    IReadOnlyCollection<ValidationFeature> SupportedFeatures { get; }
}

public enum ValidationFeature
{
    Syntax,
    Security,
    Style,
    BestPractices,
    ErrorHandling,
    CustomRules,
    Performance
}