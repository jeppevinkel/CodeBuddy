using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Models;
using System.Text.Json;
using System.IO;

namespace CodeBuddy.Core.Implementation.CodeValidation.PerformanceTesting;

public class PerformanceTestFramework
{
    private readonly ILogger _logger;
    private readonly string _historyFilePath;
    private readonly Dictionary<string, ICodeValidator> _validators;
    private readonly PerformanceTestMetricsCollector _metricsCollector;

    public PerformanceTestFramework(
        ILogger logger,
        Dictionary<string, ICodeValidator> validators,
        string historyFilePath = "performance_history.json")
    {
        _logger = logger;
        _validators = validators;
        _historyFilePath = historyFilePath;
        _metricsCollector = new PerformanceTestMetricsCollector();
    }

    public async Task<PerformanceTestReport> RunBaselineTests(Dictionary<string, string> sampleCode)
    {
        var report = new PerformanceTestReport
        {
            TestName = "Baseline Performance Tests",
            StartTime = DateTime.UtcNow,
            TestResults = new List<PerformanceTestResult>()
        };

        foreach (var (language, validator) in _validators)
        {
            if (!sampleCode.TryGetValue(language, out var code))
            {
                _logger.LogWarning("No sample code provided for language {Language}", language);
                continue;
            }

            var result = await RunSingleValidatorTest(validator, code, language);
            report.TestResults.Add(result);
        }

        report.EndTime = DateTime.UtcNow;
        await SaveTestHistory(report);
        return report;
    }

    public async Task<PerformanceTestReport> RunStressTest(
        Dictionary<string, string> sampleCode,
        int concurrentValidations = 10,
        TimeSpan duration = default)
    {
        if (duration == default)
        {
            duration = TimeSpan.FromMinutes(5);
        }

        var report = new PerformanceTestReport
        {
            TestName = "Stress Test",
            StartTime = DateTime.UtcNow,
            TestResults = new List<PerformanceTestResult>()
        };

        foreach (var (language, validator) in _validators)
        {
            if (!sampleCode.TryGetValue(language, out var code))
            {
                continue;
            }

            var tasks = new List<Task<ValidationResult>>();
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < duration)
            {
                while (tasks.Count < concurrentValidations)
                {
                    tasks.Add(validator.ValidateAsync(code, language, new ValidationOptions
                    {
                        ValidateSyntax = true,
                        ValidateSecurity = true,
                        ValidateStyle = true,
                        ValidateBestPractices = true,
                        ValidateErrorHandling = true
                    }));
                }

                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);

                var metrics = await _metricsCollector.CollectMetrics();
                report.TestResults.Add(new PerformanceTestResult
                {
                    Language = language,
                    TestType = "ConcurrentLoad",
                    Timestamp = DateTime.UtcNow,
                    Metrics = metrics
                });
            }
        }

        report.EndTime = DateTime.UtcNow;
        await SaveTestHistory(report);
        return report;
    }

    public async Task<PerformanceTestReport> RunMemoryLeakTest(
        Dictionary<string, string> sampleCode,
        int iterations = 1000)
    {
        var report = new PerformanceTestReport
        {
            TestName = "Memory Leak Test",
            StartTime = DateTime.UtcNow,
            TestResults = new List<PerformanceTestResult>()
        };

        foreach (var (language, validator) in _validators)
        {
            if (!sampleCode.TryGetValue(language, out var code))
            {
                continue;
            }

            var initialMemory = GC.GetTotalMemory(true);
            var memoryReadings = new List<long>();

            for (int i = 0; i < iterations; i++)
            {
                await validator.ValidateAsync(code, language, new ValidationOptions());
                
                if (i % 100 == 0)
                {
                    GC.Collect();
                    var currentMemory = GC.GetTotalMemory(true);
                    memoryReadings.Add(currentMemory);

                    var metrics = await _metricsCollector.CollectMetrics();
                    report.TestResults.Add(new PerformanceTestResult
                    {
                        Language = language,
                        TestType = "MemoryLeak",
                        Timestamp = DateTime.UtcNow,
                        Metrics = metrics,
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "IterationNumber", i },
                            { "MemoryUsage", currentMemory }
                        }
                    });
                }
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryGrowth = finalMemory - initialMemory;

            if (memoryGrowth > 1024 * 1024) // 1MB threshold
            {
                _logger.LogWarning("Potential memory leak detected in {Language} validator. Memory growth: {Growth:N0} bytes",
                    language, memoryGrowth);
            }
        }

        report.EndTime = DateTime.UtcNow;
        await SaveTestHistory(report);
        return report;
    }

    public async Task<PerformanceComparisonReport> CompareWithBaseline(PerformanceTestReport currentReport)
    {
        var history = await LoadTestHistory();
        var baselineReport = history.FirstOrDefault(h => h.TestName == "Baseline Performance Tests");

        if (baselineReport == null)
        {
            throw new InvalidOperationException("No baseline test results found");
        }

        return new PerformanceComparisonReport
        {
            CurrentReport = currentReport,
            BaselineReport = baselineReport,
            Differences = CalculatePerformanceDifferences(currentReport, baselineReport)
        };
    }

    private async Task<PerformanceTestResult> RunSingleValidatorTest(
        ICodeValidator validator,
        string code,
        string language)
    {
        var options = new ValidationOptions
        {
            ValidateSyntax = true,
            ValidateSecurity = true,
            ValidateStyle = true,
            ValidateBestPractices = true,
            ValidateErrorHandling = true
        };

        await validator.ValidateAsync(code, language, options);
        var metrics = await _metricsCollector.CollectMetrics();

        return new PerformanceTestResult
        {
            Language = language,
            TestType = "Baseline",
            Timestamp = DateTime.UtcNow,
            Metrics = metrics
        };
    }

    private async Task SaveTestHistory(PerformanceTestReport report)
    {
        var history = await LoadTestHistory();
        history.Add(report);

        // Keep only last 100 test runs to manage file size
        if (history.Count > 100)
        {
            history = history.OrderByDescending(h => h.StartTime).Take(100).ToList();
        }

        await File.WriteAllTextAsync(_historyFilePath, JsonSerializer.Serialize(history));
    }

    private async Task<List<PerformanceTestReport>> LoadTestHistory()
    {
        if (!File.Exists(_historyFilePath))
        {
            return new List<PerformanceTestReport>();
        }

        var content = await File.ReadAllTextAsync(_historyFilePath);
        return JsonSerializer.Deserialize<List<PerformanceTestReport>>(content) 
               ?? new List<PerformanceTestReport>();
    }

    private Dictionary<string, PerformanceDifference> CalculatePerformanceDifferences(
        PerformanceTestReport current,
        PerformanceTestReport baseline)
    {
        var differences = new Dictionary<string, PerformanceDifference>();

        foreach (var currentResult in current.TestResults)
        {
            var baselineResult = baseline.TestResults.FirstOrDefault(r => 
                r.Language == currentResult.Language && r.TestType == currentResult.TestType);

            if (baselineResult == null) continue;

            differences[$"{currentResult.Language}_{currentResult.TestType}"] = new PerformanceDifference
            {
                Language = currentResult.Language,
                TestType = currentResult.TestType,
                CpuUtilizationDelta = currentResult.Metrics.CpuUtilizationPercent - baselineResult.Metrics.CpuUtilizationPercent,
                MemoryUsageDelta = currentResult.Metrics.PeakMemoryUsageBytes - baselineResult.Metrics.PeakMemoryUsageBytes,
                ExecutionTimeDelta = currentResult.Metrics.ExecutionTimeMs - baselineResult.Metrics.ExecutionTimeMs,
                Regression = IsPerformanceRegression(currentResult.Metrics, baselineResult.Metrics)
            };
        }

        return differences;
    }

    private bool IsPerformanceRegression(PerformanceMetrics current, PerformanceMetrics baseline)
    {
        const double threshold = 1.1; // 10% degradation threshold

        return current.ExecutionTimeMs > baseline.ExecutionTimeMs * threshold ||
               current.PeakMemoryUsageBytes > baseline.PeakMemoryUsageBytes * threshold ||
               current.CpuUtilizationPercent > baseline.CpuUtilizationPercent * threshold;
    }
}