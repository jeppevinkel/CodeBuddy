using System;
using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Models.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Logging
{
    public static class ResourceLoggingServiceCollectionExtensions
    {
        public static IServiceCollection AddResourceLogging(
            this IServiceCollection services,
            Action<ResourceLoggingConfig> configure = null)
        {
            var config = new ResourceLoggingConfig();
            configure?.Invoke(config);

            services.AddSingleton(config);
            services.AddSingleton<IResourceLoggingService, ResourceLoggingService>();

            return services;
        }
    }
}