using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation.CodeValidation;

namespace CodeBuddy.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCodeBuddyCore(this IServiceCollection services)
    {
        // Core services
        services.AddScoped<IConfigurationManager, ConfigurationManager>();
        services.AddScoped<IPluginManager, PluginManager>();
        services.AddScoped<IPluginState, DefaultPluginState>();
        services.AddScoped<IPluginConfiguration, DefaultPluginConfiguration>();
        services.AddScoped<ITemplateManager, TemplateManager>();
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IFileOperations, FileOperations>();

        // Validation services
        services.AddSingleton<PredictiveResourceManager>();
        
        return services;
    }
}