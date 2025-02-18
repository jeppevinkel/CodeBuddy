using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public delegate Task<ValidationResult> ValidationDelegate(ValidationContext context);

public interface IValidationMiddleware
{
    string Name { get; }
    int Order { get; }
    Task<ValidationResult> ProcessAsync(ValidationContext context, ValidationDelegate next);
    
    // New resilience-related properties
    bool SupportsRetry { get; }
    bool RequiresCleanup { get; }
    Task CleanupAsync(ValidationContext context);
    
    // Optional methods with default implementations
    bool ShouldRetry(Exception ex) => true;
    Task OnFailureAsync(ValidationContext context, Exception ex) => Task.CompletedTask;
    Task OnSuccessAsync(ValidationContext context) => Task.CompletedTask;
    
    // Timeout configuration
    TimeSpan? Timeout => null; // Use pipeline default if null
}

public abstract class BaseValidationMiddleware : IValidationMiddleware
{
    public abstract string Name { get; }
    public abstract int Order { get; }
    public virtual bool SupportsRetry => true;
    public virtual bool RequiresCleanup => false;
    public virtual TimeSpan? Timeout => null;

    public abstract Task<ValidationResult> ProcessAsync(ValidationContext context, ValidationDelegate next);
    
    public virtual Task CleanupAsync(ValidationContext context) => Task.CompletedTask;
    public virtual bool ShouldRetry(Exception ex) => true;
    public virtual Task OnFailureAsync(ValidationContext context, Exception ex) => Task.CompletedTask;
    public virtual Task OnSuccessAsync(ValidationContext context) => Task.CompletedTask;
    
    protected async Task<ValidationResult> ExecuteWithMetrics(ValidationContext context, Func<Task<ValidationResult>> action)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var result = await action();
            var duration = DateTime.UtcNow - startTime;
            context.TrackMiddlewareExecution(Name, duration);
            await OnSuccessAsync(context);
            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            context.TrackMiddlewareExecution(Name, duration);
            await OnFailureAsync(context, ex);
            throw;
        }
    }
}