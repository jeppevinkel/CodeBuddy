using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidationContext
{
    public string Code { get; set; }
    public string Language { get; set; }
    public ValidationOptions Options { get; set; }
    public ICodeValidator Validator { get; set; }
    public ValidationResult Result { get; set; }
    
    // Resilience tracking
    public Dictionary<string, object> Items { get; } = new();
    public CancellationToken CancellationToken { get; set; }
    public DateTime StartTime { get; set; }
    public ValidationResilienceConfig ResilienceConfig { get; set; }

    public ValidationContext(
        string code,
        string language,
        ValidationOptions options,
        ICodeValidator validator,
        ValidationResilienceConfig resilienceConfig = null)
    {
        Code = code;
        Language = language;
        Options = options;
        Validator = validator;
        Result = new ValidationResult();
        StartTime = DateTime.UtcNow;
        ResilienceConfig = resilienceConfig ?? new ValidationResilienceConfig();
    }

    public T GetItem<T>(string key) where T : class
    {
        return Items.TryGetValue(key, out var value) ? value as T : null;
    }

    public void SetItem<T>(string key, T value) where T : class
    {
        Items[key] = value;
    }

    public void TrackMiddlewareExecution(string middlewareName, TimeSpan duration)
    {
        if (Result.RecoveryStats.DetectedPatterns == null)
        {
            Result.RecoveryStats.DetectedPatterns = new List<FailurePattern>();
        }

        Result.RecoveryStats.PerformanceImpactMs += duration.TotalMilliseconds;
    }
}