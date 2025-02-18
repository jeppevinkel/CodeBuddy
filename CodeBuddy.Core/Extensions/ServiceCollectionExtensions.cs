using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyCore(this IServiceCollection services)
        {
            // Error handling and analytics services
            services.AddScoped<IErrorHandlingService, ErrorHandlingService>();
            services.AddScoped<IErrorAnalyticsService, ErrorAnalyticsService>();
            services.AddScoped<IErrorMonitoringDashboard, ErrorMonitoringDashboard>();

            // Time series storage for analytics
            services.AddSingleton<ITimeSeriesStorage, TimeSeriesStorage>();

            return services;
        }
    }
}