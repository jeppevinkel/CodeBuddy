using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidationPipeline
{
    private readonly ILogger<ValidationPipeline> _logger;
    private readonly List<IValidationMiddleware> _middleware;

    public ValidationPipeline(ILogger<ValidationPipeline> logger)
    {
        _logger = logger;
        _middleware = new List<IValidationMiddleware>();
    }

    public void AddMiddleware(IValidationMiddleware middleware)
    {
        _middleware.Add(middleware);
        _middleware.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public async Task<ValidationResult> ExecuteAsync(ValidationContext context)
    {
        ValidationDelegate pipeline = BuildPipeline(context);
        return await pipeline(context);
    }

    private ValidationDelegate BuildPipeline(ValidationContext context)
    {
        ValidationDelegate pipeline = async (ctx) =>
        {
            return await context.Validator.ValidateAsync(ctx.Code, ctx.Language, ctx.Options);
        };

        // Build the pipeline in reverse order
        for (int i = _middleware.Count - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = pipeline;
            pipeline = async (ctx) =>
            {
                try
                {
                    return await middleware.ProcessAsync(ctx, next);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in middleware {MiddlewareName}", middleware.Name);
                    throw;
                }
            };
        }

        return pipeline;
    }
}