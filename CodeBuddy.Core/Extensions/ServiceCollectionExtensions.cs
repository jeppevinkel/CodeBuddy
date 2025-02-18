using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddResourceAlertSystem(this IServiceCollection services, Action<AlertConfiguration> configure = null)
        {
            var config = new AlertConfiguration();
            configure?.Invoke(config);

            // Register default thresholds if not configured
            if (config.Thresholds.Count == 0)
            {
                config.Thresholds[ResourceMetricType.CPU] = new ResourceThreshold
                {
                    MetricType = ResourceMetricType.CPU,
                    WarningThreshold = 70,
                    CriticalThreshold = 85,
                    EmergencyThreshold = 95,
                    SustainedDuration = TimeSpan.FromMinutes(5),
                    RateOfChangeThreshold = 10 // percent per minute
                };

                config.Thresholds[ResourceMetricType.Memory] = new ResourceThreshold
                {
                    MetricType = ResourceMetricType.Memory,
                    WarningThreshold = 70,
                    CriticalThreshold = 85,
                    EmergencyThreshold = 95,
                    SustainedDuration = TimeSpan.FromMinutes(5),
                    RateOfChangeThreshold = 100 // MB per minute
                };

                config.Thresholds[ResourceMetricType.DiskIO] = new ResourceThreshold
                {
                    MetricType = ResourceMetricType.DiskIO,
                    WarningThreshold = 50,
                    CriticalThreshold = 75,
                    EmergencyThreshold = 90,
                    SustainedDuration = TimeSpan.FromMinutes(2),
                    RateOfChangeThreshold = 50 // MB/s per minute
                };
            }

            services.AddSingleton(config);
            services.AddSingleton<IResourceAlertManager, ResourceAlertManager>();

            return services;
        }

        public static IServiceCollection AddValidationCache(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ValidationCacheConfig>(configuration.GetSection("ValidationCache"));
            services.AddSingleton<IValidationCache, ValidationCache>();
            return services;
        }
    }
}