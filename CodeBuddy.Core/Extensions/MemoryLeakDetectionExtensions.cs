using System;
using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Middleware;

namespace CodeBuddy.Core.Extensions;

public static class MemoryLeakDetectionExtensions
{
    public static IServiceCollection AddMemoryLeakDetection(
        this IServiceCollection services,
        Action<MemoryLeakConfig> configure = null)
    {
        var config = new MemoryLeakConfig();
        configure?.Invoke(config);

        services.AddSingleton(config);
        services.AddSingleton<IMemoryLeakDetector, MemoryLeakDetector>();
        services.AddSingleton<IValidationMiddleware, MemoryLeakDetectionMiddleware>();

        return services;
    }
}