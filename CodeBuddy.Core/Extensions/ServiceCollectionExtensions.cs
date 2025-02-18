using CodeBuddy.Core.Implementation.Monitoring;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddResourceMonitoring(this IServiceCollection services)
        {
            services.AddSingleton<IResourceMonitoringDashboard, ResourceMonitoringDashboard>();
            return services;
        }
    }
}