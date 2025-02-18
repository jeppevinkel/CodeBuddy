using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

namespace CodeBuddy.Core.Implementation.CodeValidation.Middleware;

public class MemoryLeakDetectionMiddleware : IValidationMiddleware
{
    private readonly ILogger<MemoryLeakDetectionMiddleware> _logger;
    private readonly IMemoryLeakDetector _memoryLeakDetector;
    private readonly MemoryLeakConfig _config;

    public string Name => "MemoryLeakDetection";
    public int Order => 100;  // Run early in the pipeline to monitor the entire validation process

    public MemoryLeakDetectionMiddleware(
        ILogger<MemoryLeakDetectionMiddleware> logger,
        IMemoryLeakDetector memoryLeakDetector,
        MemoryLeakConfig config)
    {
        _logger = logger;
        _memoryLeakDetector = memoryLeakDetector;
        _config = config;
    }

    public async Task<ValidationResult> ProcessAsync(ValidationContext context, ValidationDelegate next)
    {
        var componentId = $"Validation_{context.Id}";

        try
        {
            // Start memory monitoring
            await _memoryLeakDetector.StartMonitoringAsync(componentId);

            // Execute the validation pipeline
            var result = await next(context);

            // Perform memory analysis
            var memoryAnalysis = await _memoryLeakDetector.AnalyzeMemoryPatternsAsync(componentId);
            var healthReport = await _memoryLeakDetector.GenerateHealthReportAsync(componentId);

            // Add memory analysis data to validation result
            result.AdditionalData["MemoryAnalysis"] = new
            {
                LeakDetected = memoryAnalysis.LeakDetected,
                ConfidenceLevel = memoryAnalysis.ConfidenceLevel,
                HealthStatus = healthReport.OverallHealth.ToString(),
                Recommendations = healthReport.Recommendations,
                MemoryTrends = healthReport.GenerationTrends
            };

            // If memory leak detected with high confidence, add warning
            if (memoryAnalysis.LeakDetected && memoryAnalysis.ConfidenceLevel >= _config.LeakConfidenceThreshold)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"Potential memory leak detected (Confidence: {memoryAnalysis.ConfidenceLevel}%)",
                    Category = "Memory",
                    Context = new Dictionary<string, string>
                    {
                        ["HealthStatus"] = healthReport.OverallHealth.ToString(),
                        ["StartTime"] = healthReport.TimePeriod.Start.ToString("O"),
                        ["EndTime"] = healthReport.TimePeriod.End.ToString("O")
                    }
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in memory leak detection middleware");
            throw;
        }
        finally
        {
            // Always stop monitoring
            await _memoryLeakDetector.StopMonitoringAsync(componentId);
        }
    }
}