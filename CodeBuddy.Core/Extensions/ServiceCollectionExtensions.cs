using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Implementation.Documentation;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyServices(this IServiceCollection services)
        {
            // Core services
            services.AddScoped<IPluginManager, PluginManager>();
            services.AddScoped<IPluginConfiguration, DefaultPluginConfiguration>();
            services.AddScoped<IPluginState, DefaultPluginState>();
            services.AddScoped<IPluginAuthService, DefaultPluginAuthService>();
            services.AddScoped<IConfigurationManager, ConfigurationManager>();
            services.AddScoped<ICodeGenerator, CodeGenerator>();
            services.AddScoped<IFileOperations, FileOperations>();
            services.AddScoped<ITemplateManager, TemplateManager>();
            services.AddScoped<IDocumentationGenerator, DocumentationGenerator>();
            
            return services;
        }
    }
}