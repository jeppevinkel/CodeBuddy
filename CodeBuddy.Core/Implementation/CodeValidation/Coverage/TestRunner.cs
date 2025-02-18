using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace CodeBuddy.Core.Implementation.CodeValidation.Coverage;

public class TestRunner : ITestRunner
{
    private readonly ITestDiscoveryService _testDiscoveryService;
    private readonly ICoverageTracker _coverageTracker;
    private readonly ILogger _logger;
    
    public TestRunner(
        ITestDiscoveryService testDiscoveryService,
        ICoverageTracker coverageTracker,
        ILogger logger)
    {
        _testDiscoveryService = testDiscoveryService;
        _coverageTracker = coverageTracker;
        _logger = logger;
    }

    public async Task RunTestsAsync(string projectPath)
    {
        // Initialize coverage tracking
        _coverageTracker.Initialize();

        try
        {
            // Discover test projects
            var testProjects = await _testDiscoveryService.DiscoverTestProjectsAsync(projectPath);

            foreach (var testProject in testProjects)
            {
                await RunTestProjectAsync(testProject);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error running tests: {ex.Message}", ex);
            throw new TestExecutionException("Failed to execute tests", ex);
        }
    }

    private async Task RunTestProjectAsync(TestProject testProject)
    {
        _logger.Info($"Running tests for project: {testProject.Name}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testProject.ProjectPath}\" --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var output = new List<string>();
        var errors = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.Add(e.Data);
                _logger.Debug($"Test output: {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errors.Add(e.Data);
                _logger.Error($"Test error: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            throw new TestExecutionException(
                $"Tests failed for project {testProject.Name}. Exit code: {process.ExitCode}{Environment.NewLine}{errorMessage}");
        }

        _logger.Info($"Successfully completed tests for project: {testProject.Name}");
    }
}

public interface ITestDiscoveryService
{
    Task<List<TestProject>> DiscoverTestProjectsAsync(string projectPath);
}

public interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Error(string message, Exception ex = null);
}

public class TestProject
{
    public string Name { get; set; }
    public string ProjectPath { get; set; }
    public TestType Type { get; set; }
}

public enum TestType
{
    Unit,
    Integration,
    E2E
}

public class TestExecutionException : Exception
{
    public TestExecutionException(string message) : base(message) { }
    public TestExecutionException(string message, Exception innerException) : base(message, innerException) { }
}