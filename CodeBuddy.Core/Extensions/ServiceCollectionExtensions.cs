using System;
using CodeBuddy.Core.Implementation.Logging;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyLogging(this IServiceCollection services, Action<LoggingConfiguration> configure = null)
        {
            var config = new LoggingConfiguration();
            configure?.Invoke(config);

            services.AddSingleton(config);
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<LoggingDashboard>();
            services.AddSingleton<ErrorLoggingMiddleware>();

            return services;
        }
    }
}