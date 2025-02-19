using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Models.ValidationModels;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class ValidationPipeline
    {
        private readonly IValidatorRegistry _validatorRegistry;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ResourceLeakDetectionSystem _resourceLeakDetector;

        public ValidationPipeline(
            IValidatorRegistry validatorRegistry,
            PerformanceMonitor performanceMonitor,
            ResourceLeakDetectionSystem resourceLeakDetector)
        {
            _validatorRegistry = validatorRegistry;
            _performanceMonitor = performanceMonitor;
            _resourceLeakDetector = resourceLeakDetector;
        }

        public async Task<ValidationResult> ValidateAsync(ValidationContext context)
        {
            using var resourceScope = _resourceLeakDetector.TrackResource(
                $"validation_{context.Id}",
                "ValidationPipeline",
                context);

            var result = new ValidationResult();

            try
            {
                // Start performance monitoring
                using var perfScope = await _performanceMonitor.StartMonitoringAsync(context);

                // Get appropriate validator
                var validator = await _validatorRegistry.GetValidatorAsync(context.Language);
                if (validator == null)
                {
                    result.AddError($"No validator found for language: {context.Language}");
                    return result;
                }

                // Monitor resource usage during validation
                var resourceAnalysis = await _resourceLeakDetector.AnalyzeResourceUsageAsync(context.Id);
                if (resourceAnalysis.MemoryLeakDetected || 
                    resourceAnalysis.ResourceTypes.Values.Any(r => r.LeakProbability > 0.7))
                {
                    // Attempt auto-recovery if configured
                    if (context.Config.EnableAutoRecovery)
                    {
                        await _resourceLeakDetector.TryAutoRecoverAsync(context.Id, resourceAnalysis);
                    }

                    // Add warnings about potential resource leaks
                    foreach (var resourceType in resourceAnalysis.ResourceTypes)
                    {
                        if (resourceType.Value.LeakProbability > 0.7)
                        {
                            result.AddWarning(
                                $"Potential {resourceType.Key} leak detected " +
                                $"(probability: {resourceType.Value.LeakProbability:P0})");
                        }
                    }
                }

                // Perform validation
                var validationResult = await validator.ValidateAsync(context);
                result.Merge(validationResult);

                // Check for plugin-specific resource leaks
                if (resourceAnalysis.PluginResourcePatterns?.Any() == true)
                {
                    foreach (var plugin in resourceAnalysis.PluginResourcePatterns)
                    {
                        result.AddWarning(
                            $"Plugin {plugin.Key} shows potential resource leaks " +
                            $"(probability: {plugin.Value.LeakProbability:P0})");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.AddError($"Validation failed: {ex.Message}");
                return result;
            }
        }
    }
}