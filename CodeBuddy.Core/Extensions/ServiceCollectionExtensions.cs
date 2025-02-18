using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation.PatternDetection;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyServices(this IServiceCollection services)
        {
            services.AddScoped<IPluginManager, PluginManager>();
            services.AddScoped<IPluginState, DefaultPluginState>();
            services.AddScoped<IPluginConfiguration, DefaultPluginConfiguration>();
            
            // Register Pattern Detection System
            services.AddSingleton<IPatternRepository>(sp => 
                new PatternRepository("Patterns"));
            services.AddScoped<IPatternMatchingEngine, PatternMatchingEngine>();
            
            return services;
        }
    }
}