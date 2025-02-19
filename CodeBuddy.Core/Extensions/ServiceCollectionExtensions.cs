using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyCore(this IServiceCollection services)
        {
            // Error handling services
            services.AddSingleton<IPreemptiveErrorHandler, PreemptiveErrorHandler>();
            services.AddSingleton<IErrorMonitoringDashboard, ErrorMonitoringDashboard>();
            services.AddSingleton<IErrorAnalyticsService, ErrorAnalyticsService>();
            
            // Time series storage
            services.AddSingleton<ITimeSeriesStorageOptions, TimeSeriesStorageOptions>();
            services.AddSingleton<ITimeSeriesStorage, TimeSeriesStorage>();

            return services;
        }
    }
}