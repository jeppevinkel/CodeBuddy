using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Represents application configuration with validation and intelligent defaults.
/// This class includes built-in validation rules and environment-specific configurations.
/// </summary>
public class Configuration
{
    /// <summary>
    /// Directory containing code templates. Must be an existing, readable directory.
    /// Default: "./templates" in development, "/opt/codebuddy/templates" in production
    /// </summary>
    [Required(ErrorMessage = "Templates directory path is required")]
    [DirectoryExists(ErrorMessage = "Templates directory must exist and be accessible")]
    public string TemplatesDirectory { get; set; }

    /// <summary>
    /// Directory for generated output. Must be writable with sufficient disk space.
    /// Default: "./output" in development, "/var/codebuddy/output" in production
    /// Minimum required disk space: 500MB
    /// </summary>
    [Required(ErrorMessage = "Output directory path is required")]
    [DirectoryExists(ErrorMessage = "Output directory must exist and be accessible")]
    [MinimumDiskSpace(500, ErrorMessage = "Output directory must have at least 500MB free space")]
    public string OutputDirectory { get; set; }

    /// <summary>
    /// Directory containing plugin assemblies. Must be a readable directory.
    /// Default: "./plugins" in development, "/opt/codebuddy/plugins" in production
    /// </summary>
    [Required(ErrorMessage = "Plugins directory path is required")]
    [DirectoryExists(ErrorMessage = "Plugins directory must exist and be accessible")]
    public string PluginsDirectory { get; set; }

    /// <summary>
    /// Default parameters used across the application.
    /// These parameters can be overridden by environment-specific settings.
    /// </summary>
    [ValidateParameterNames(ErrorMessage = "Parameter names must be alphanumeric and start with a letter")]
    public Dictionary<string, string> DefaultParameters { get; set; } = new();

    /// <summary>
    /// Minimum log level for application logging.
    /// Default: Information in development, Warning in production
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Controls whether plugins are enabled.
    /// Default: true in development, false in production (requires explicit enable)
    /// </summary>
    public bool EnablePlugins { get; set; } = true;

    /// <summary>
    /// Maximum allowed plugin count to prevent resource exhaustion.
    /// Default: 50 in development, 200 in production
    /// </summary>
    [Range(1, 1000, ErrorMessage = "Maximum plugin count must be between 1 and 1000")]
    public int MaxPluginCount { get; set; } = 50;

    /// <summary>
    /// Creates a new Configuration instance with environment-specific defaults
    /// </summary>
    public Configuration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        // Set environment-specific defaults
        switch (environment.ToLowerInvariant())
        {
            case "production":
                TemplatesDirectory = "/opt/codebuddy/templates";
                OutputDirectory = "/var/codebuddy/output";
                PluginsDirectory = "/opt/codebuddy/plugins";
                MinimumLogLevel = LogLevel.Warning;
                EnablePlugins = false;
                MaxPluginCount = 200;
                break;

            case "staging":
                TemplatesDirectory = "/opt/codebuddy/templates";
                OutputDirectory = "/var/codebuddy/output";
                PluginsDirectory = "/opt/codebuddy/plugins";
                MinimumLogLevel = LogLevel.Debug;
                EnablePlugins = true;
                MaxPluginCount = 100;
                break;

            default: // Development
                TemplatesDirectory = "./templates";
                OutputDirectory = "./output";
                PluginsDirectory = "./plugins";
                MinimumLogLevel = LogLevel.Information;
                EnablePlugins = true;
                MaxPluginCount = 50;
                break;
        }
    }

    /// <summary>
    /// Validates the configuration and returns a list of validation errors
    /// </summary>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);
        return results;
    }
}

/// <summary>
/// Validates that a directory exists and is accessible
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DirectoryExistsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("Directory path cannot be empty");
        }

        var path = value.ToString()!;
        if (!Directory.Exists(path))
        {
            return new ValidationResult($"Directory does not exist: {path}");
        }

        try
        {
            // Test read access
            Directory.GetFiles(path);
            return ValidationResult.Success;
        }
        catch (UnauthorizedAccessException)
        {
            return new ValidationResult($"Access denied to directory: {path}");
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Error accessing directory {path}: {ex.Message}");
        }
    }
}

/// <summary>
/// Validates that a directory has sufficient free disk space (in MB)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MinimumDiskSpaceAttribute : ValidationAttribute
{
    private readonly long _requiredMegabytes;

    public MinimumDiskSpaceAttribute(long requiredMegabytes)
    {
        _requiredMegabytes = requiredMegabytes;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("Directory path cannot be empty");
        }

        var path = value.ToString()!;
        if (!Directory.Exists(path))
        {
            return new ValidationResult($"Directory does not exist: {path}");
        }

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);
            var freeSpaceMB = driveInfo.AvailableFreeSpace / (1024 * 1024);
            
            return freeSpaceMB >= _requiredMegabytes 
                ? ValidationResult.Success 
                : new ValidationResult($"Insufficient disk space. Required: {_requiredMegabytes}MB, Available: {freeSpaceMB}MB");
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Error checking disk space for {path}: {ex.Message}");
        }
    }
}

/// <summary>
/// Validates parameter names in the configuration dictionary
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ValidateParameterNamesAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not Dictionary<string, string> parameters)
        {
            return ValidationResult.Success;
        }

        var invalidNames = parameters.Keys
            .Where(key => !IsValidParameterName(key))
            .ToList();

        return invalidNames.Count == 0
            ? ValidationResult.Success
            : new ValidationResult($"Invalid parameter names: {string.Join(", ", invalidNames)}");
    }

    private static bool IsValidParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]))
        {
            return false;
        }

        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}