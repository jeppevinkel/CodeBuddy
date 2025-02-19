using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Implementation.Configuration;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyCore(this IServiceCollection services)
        {
            services.AddScoped<IConfigurationManager, ConfigurationManager>();
            services.AddScoped<IConfigurationValidator, ConfigurationValidator>();
            services.AddScoped<IConfigurationDashboard, ConfigurationDashboard>();
            services.AddScoped<IConfigurationMigrationManager, ConfigurationMigrationManager>();
            services.AddScoped<ILoggingService, LoggingService>();
            
            return services;
        }
    }
}