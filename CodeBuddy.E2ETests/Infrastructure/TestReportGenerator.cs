using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using Newtonsoft.Json;

namespace CodeBuddy.E2ETests.Infrastructure
{
    public class TestReportGenerator
    {
        private readonly string _reportDirectory;
        private readonly List<TestResult> _testResults;

        public TestReportGenerator(string reportDirectory)
        {
            _reportDirectory = reportDirectory;
            _testResults = new List<TestResult>();
            
            if (!Directory.Exists(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }
        }

        public void AddTestResult(string testName, bool success, TimeSpan duration, 
            ResourceMetrics resourceMetrics, string errorMessage = null)
        {
            _testResults.Add(new TestResult
            {
                TestName = testName,
                Success = success,
                Duration = duration,
                ResourceMetrics = resourceMetrics,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task GenerateReportAsync()
        {
            var reportPath = Path.Combine(_reportDirectory, $"e2e_test_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            var summaryPath = Path.Combine(_reportDirectory, $"e2e_test_summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");

            // Generate detailed JSON report
            var report = new TestReport
            {
                TotalTests = _testResults.Count,
                SuccessfulTests = _testResults.Count(r => r.Success),
                FailedTests = _testResults.Count(r => !r.Success),
                TotalDuration = TimeSpan.FromTicks(_testResults.Sum(r => r.Duration.Ticks)),
                AverageTestDuration = TimeSpan.FromTicks((long)_testResults.Average(r => r.Duration.Ticks)),
                TestResults = _testResults,
                ResourceUtilization = new ResourceUtilizationSummary
                {
                    TotalMemoryUsed = _testResults.Sum(r => r.ResourceMetrics?.MemoryUsed ?? 0),
                    PeakMemoryUsage = _testResults.Max(r => r.ResourceMetrics?.MemoryUsed ?? 0),
                    AverageProcessingTime = TimeSpan.FromTicks((long)_testResults
                        .Where(r => r.ResourceMetrics != null)
                        .Average(r => r.ResourceMetrics.ProcessingTime.Ticks))
                }
            };

            await File.WriteAllTextAsync(reportPath, 
                JsonConvert.SerializeObject(report, Formatting.Indented));

            // Generate human-readable summary
            var summary = new StringBuilder();
            summary.AppendLine("End-to-End Test Execution Summary");
            summary.AppendLine("================================");
            summary.AppendLine($"Total Tests: {report.TotalTests}");
            summary.AppendLine($"Successful: {report.SuccessfulTests}");
            summary.AppendLine($"Failed: {report.FailedTests}");
            summary.AppendLine($"Success Rate: {(report.SuccessfulTests * 100.0 / report.TotalTests):F1}%");
            summary.AppendLine();
            summary.AppendLine("Resource Utilization");
            summary.AppendLine("-------------------");
            summary.AppendLine($"Total Memory Used: {report.ResourceUtilization.TotalMemoryUsed / 1024 / 1024:F2} MB");
            summary.AppendLine($"Peak Memory Usage: {report.ResourceUtilization.PeakMemoryUsage / 1024 / 1024:F2} MB");
            summary.AppendLine($"Average Processing Time: {report.ResourceUtilization.AverageProcessingTime.TotalSeconds:F2}s");
            summary.AppendLine();
            
            if (report.FailedTests > 0)
            {
                summary.AppendLine("Failed Tests");
                summary.AppendLine("------------");
                foreach (var failure in _testResults.Where(r => !r.Success))
                {
                    summary.AppendLine($"- {failure.TestName}");
                    summary.AppendLine($"  Error: {failure.ErrorMessage}");
                }
            }

            await File.WriteAllTextAsync(summaryPath, summary.ToString());
        }

        private class TestResult
        {
            public string TestName { get; set; }
            public bool Success { get; set; }
            public TimeSpan Duration { get; set; }
            public ResourceMetrics ResourceMetrics { get; set; }
            public string ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class TestReport
        {
            public int TotalTests { get; set; }
            public int SuccessfulTests { get; set; }
            public int FailedTests { get; set; }
            public TimeSpan TotalDuration { get; set; }
            public TimeSpan AverageTestDuration { get; set; }
            public List<TestResult> TestResults { get; set; }
            public ResourceUtilizationSummary ResourceUtilization { get; set; }
        }

        private class ResourceUtilizationSummary
        {
            public long TotalMemoryUsed { get; set; }
            public long PeakMemoryUsage { get; set; }
            public TimeSpan AverageProcessingTime { get; set; }
        }
    }
}