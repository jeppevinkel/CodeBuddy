using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Generates comprehensive usage examples for core features
    /// </summary>
    public class UsageExampleGenerator
    {
        private readonly IPluginManager _pluginManager;
        private readonly IConfigurationManager _configManager;
        private readonly IFileOperations _fileOps;

        public UsageExampleGenerator(
            IPluginManager pluginManager,
            IConfigurationManager configManager,
            IFileOperations fileOps)
        {
            _pluginManager = pluginManager;
            _configManager = configManager;
            _fileOps = fileOps;
        }

        /// <summary>
        /// Generates examples for core validation features
        /// </summary>
        public async Task<List<CodeExample>> GenerateValidationExamplesAsync()
        {
            var examples = new List<CodeExample>
            {
                // Basic validation example
                new CodeExample
                {
                    Title = "Basic Code Validation",
                    Description = "Shows how to validate code using the default validator",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.CodeValidation;

public async Task ValidateCodeAsync()
{
    var validator = new CSharpCodeValidator();
    var code = @""
        public class Example 
        {
            public void ProcessData() 
            {
                var data = LoadData();
                data.Process();
                data.Dispose();
            }
        }"";

    var result = await validator.ValidateAsync(code);
    if (!result.IsValid)
    {
        foreach (var error in result.Errors)
        {
            Console.WriteLine($""{error.Line}: {error.Message}"");
        }
    }
}"
                },

                // Resource management example
                new CodeExample
                {
                    Title = "Resource Management",
                    Description = "Demonstrates proper resource handling patterns",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;

public async Task ManageResourcesAsync()
{
    var manager = new ResourcePreallocationManager();
    
    // Pre-allocate resources
    await manager.PreallocateAsync(new ResourceAllocationConfig
    {
        MemoryLimit = 1024 * 1024 * 100, // 100MB
        ThreadPoolSize = 4,
        CacheSize = 1000
    });

    try
    {
        // Use resources
        await ProcessDataAsync();
    }
    finally
    {
        // Release resources
        await manager.ReleaseAsync();
    }
}"
                },

                // Plugin system example
                new CodeExample
                {
                    Title = "Plugin Integration",
                    Description = "Shows how to create and register a custom plugin",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Interfaces;

public class CustomValidator : IPlugin
{
    public string Name => ""CustomValidator"";
    public string Description => ""Custom validation rules for specific patterns"";
    public Version Version => new Version(1, 0, 0);

    public async Task<ValidationResult> ValidateAsync(string code)
    {
        // Custom validation logic
        return new ValidationResult 
        {
            IsValid = true,
            Rules = new[] { ""CUSTOM001"", ""CUSTOM002"" }
        };
    }
}

// Register plugin
public async Task RegisterPluginAsync()
{
    var pluginManager = new PluginManager();
    await pluginManager.RegisterPluginAsync(new CustomValidator());
}"
                },

                // Cross-language validation example
                new CodeExample
                {
                    Title = "Cross-Language Validation",
                    Description = "Demonstrates validation across multiple languages",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.CodeValidation;

public async Task ValidateMultiLanguageAsync()
{
    var validators = new Dictionary<string, ICodeValidator>
    {
        ["".cs""] = new CSharpCodeValidator(),
        ["".js""] = new JavaScriptCodeValidator(),
        ["".py""] = new PythonCodeValidator()
    };

    foreach (var file in Directory.GetFiles(""./src"", ""*.*"", SearchOption.AllDirectories))
    {
        var ext = Path.GetExtension(file);
        if (validators.TryGetValue(ext, out var validator))
        {
            var code = await File.ReadAllTextAsync(file);
            var result = await validator.ValidateAsync(code);
            LogValidationResult(file, result);
        }
    }
}"
                },

                // Performance monitoring example
                new CodeExample
                {
                    Title = "Performance Monitoring",
                    Description = "Shows how to monitor validation performance",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;

public async Task MonitorPerformanceAsync()
{
    var monitor = new PerformanceMonitor();
    var dashboard = new ValidationPipelineDashboard();

    monitor.OnMetricsCollected += (metrics) =>
    {
        dashboard.UpdateMetrics(metrics);
        if (metrics.MemoryUsage > metrics.MemoryThreshold)
        {
            dashboard.RaiseAlert(""High memory usage detected"");
        }
    };

    await using (monitor.TrackOperation(""BatchValidation""))
    {
        await ValidateBatchAsync(files);
    }

    var report = await dashboard.GenerateReportAsync();
    await SaveReportAsync(report);
}"
                }
            };

            return examples;
        }

        /// <summary>
        /// Generates examples for plugin development
        /// </summary>
        public async Task<List<CodeExample>> GeneratePluginExamplesAsync()
        {
            var examples = new List<CodeExample>
            {
                // Plugin creation example
                new CodeExample
                {
                    Title = "Creating a Plugin",
                    Description = "Shows how to create a new plugin with custom validation rules",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;

public class CustomPatternPlugin : IPlugin, IValidatorCapabilities
{
    public string Name => ""CustomPatternValidator"";
    public string Description => ""Validates custom code patterns"";
    public Version Version => new Version(1, 0, 0);

    public IEnumerable<string> SupportedLanguages => new[] { ""cs"", ""js"" };
    
    public async Task<ValidationResult> ValidateAsync(CodeSnippet snippet)
    {
        var result = new ValidationResult();
        
        // Custom validation logic
        var patterns = await LoadPatternsAsync();
        foreach (var pattern in patterns)
        {
            if (await MatchesPatternAsync(snippet.Code, pattern))
            {
                result.Violations.Add(new ValidationViolation
                {
                    Rule = pattern.RuleId,
                    Message = pattern.Description,
                    Severity = pattern.Severity
                });
            }
        }
        
        return result;
    }
    
    private async Task<IEnumerable<Pattern>> LoadPatternsAsync()
    {
        // Load patterns from configuration
        return await patternRepository.LoadPatternsAsync();
    }
}"
                },

                // Plugin configuration example
                new CodeExample
                {
                    Title = "Plugin Configuration",
                    Description = "Demonstrates how to configure a plugin",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation;

public class CustomPluginConfig : IPluginConfiguration
{
    public IDictionary<string, object> Settings { get; }
    
    public CustomPluginConfig()
    {
        Settings = new Dictionary<string, object>
        {
            [""MaxRules""] = 100,
            [""CacheSize""] = 1000,
            [""EnabledRules""] = new[] { ""RULE001"", ""RULE002"" }
        };
    }
    
    public T GetSetting<T>(string key, T defaultValue = default)
    {
        if (Settings.TryGetValue(key, out var value))
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return defaultValue;
    }
}"
                }
            };

            return examples;
        }

        /// <summary>
        /// Generates examples for error handling
        /// </summary>
        public async Task<List<CodeExample>> GenerateErrorHandlingExamplesAsync()
        {
            var examples = new List<CodeExample>
            {
                // Resource cleanup example
                new CodeExample
                {
                    Title = "Resource Cleanup",
                    Description = "Shows proper resource cleanup patterns",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;

public class ResourceManager : IDisposable
{
    private bool _disposed;
    private readonly IResourceCleanupService _cleanup;
    private readonly IList<IDisposable> _resources;
    
    public async Task UseResourceAsync()
    {
        try
        {
            var resource = await AcquireResourceAsync();
            _resources.Add(resource);
            await ProcessResourceAsync(resource);
        }
        catch (ResourceException ex)
        {
            // Log and handle resource acquisition failure
            await _cleanup.HandleResourceFailureAsync(ex);
            throw;
        }
        finally
        {
            await CleanupResourcesAsync();
        }
    }
    
    protected virtual async Task CleanupResourcesAsync()
    {
        foreach (var resource in _resources)
        {
            try
            {
                await _cleanup.ReleaseResourceAsync(resource);
            }
            catch (Exception ex)
            {
                // Log cleanup failure but continue with other resources
                await _cleanup.LogCleanupFailureAsync(ex);
            }
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var resource in _resources)
                {
                    resource.Dispose();
                }
            }
            _disposed = true;
        }
    }
}"
                },

                // Error recovery example
                new CodeExample
                {
                    Title = "Error Recovery",
                    Description = "Demonstrates error recovery strategies",
                    Language = "csharp",
                    Code = @"using CodeBuddy.Core.Implementation.ErrorHandling;

public class ValidationPipeline
{
    private readonly IErrorHandlingService _errorHandler;
    private readonly IValidatorRegistry _registry;
    
    public async Task<ValidationResult> ValidateWithRecoveryAsync(string code)
    {
        var retryCount = 0;
        var maxRetries = 3;
        
        while (retryCount < maxRetries)
        {
            try
            {
                var validator = await _registry.GetValidatorAsync();
                return await validator.ValidateAsync(code);
            }
            catch (ValidatorException ex)
            {
                retryCount++;
                if (retryCount == maxRetries)
                {
                    // Final retry failed, handle gracefully
                    await _errorHandler.HandleValidationFailureAsync(ex);
                    return new ValidationResult 
                    { 
                        IsValid = false,
                        Error = ""Validation failed after multiple retries""
                    };
                }
                
                // Wait before retry with exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                await _registry.ResetValidatorAsync();
            }
        }
        
        return new ValidationResult { IsValid = false };
    }
}"
                }
            };

            return examples;
        }
    }
}