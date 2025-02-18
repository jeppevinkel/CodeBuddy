using System;
using Microsoft.Extensions.DependencyInjection;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Implementation.CodeValidation;

namespace CodeBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCodeBuddyCore(this IServiceCollection services)
        {
            // Register core services
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<IConfigurationManager, ConfigurationManager>();
            services.AddSingleton<ITemplateManager, TemplateManager>();
            services.AddSingleton<IFileOperations, FileOperations>();
            services.AddSingleton<ICodeGenerator, CodeGenerator>();

            // Register error handling
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

            // Register validators
            services.AddSingleton<IValidatorRegistry, ValidatorRegistry>();
            services.AddSingleton<IValidationCache, ValidationCache>();
            
            services.AddTransient<CSharpCodeValidator>();
            services.AddTransient<JavaScriptCodeValidator>();
            services.AddTransient<PythonCodeValidator>();

            return services;
        }
    }
}