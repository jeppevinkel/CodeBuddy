using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements configuration management with validation, health checks, and intelligent defaults
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly IFileOperations _fileOperations;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _configPath;
    private readonly Dictionary<string, object> _cache = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Timer _healthCheckTimer;

    public ConfigurationManager(
        IFileOperations fileOperations, 
        ILogger<ConfigurationManager> logger,
        string configPath = "config.json")
    {
        _fileOperations = fileOperations;
        _logger = logger;
        _configPath = configPath;
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Initialize health check timer (runs every 5 minutes)
        _healthCheckTimer = new Timer(RunHealthCheck, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets configuration for a specific section with validation and caching
    /// </summary>
    public T GetConfiguration<T>(string section) where T : class, new()
    {
        try
        {
            _logger.LogDebug("Getting configuration for section {Section}", section);

            // Check cache first
            if (_cache.TryGetValue(section, out var cached))
            {
                return (T)cached;
            }

            // Load and validate configuration
            var config = LoadConfiguration<T>(section);
            
            // Validate configuration
            var validationResults = ValidateConfiguration(config);
            if (validationResults.Any())
            {
                var errors = string.Join(Environment.NewLine, validationResults.Select(r => r.ErrorMessage));
                _logger.LogWarning("Configuration validation warnings for section {Section}:{NewLine}{Errors}", 
                    section, Environment.NewLine, errors);
            }

            // Cache and return
            _cache[section] = config;
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration for section {Section}", section);
            throw new ConfigurationException($"Failed to load configuration section '{section}'", ex);
        }
    }

    /// <summary>
    /// Saves configuration with validation and schema checks
    /// </summary>
    public void SaveConfiguration<T>(string section, T configuration) where T : class
    {
        try
        {
            _logger.LogInformation("Saving configuration for section {Section}", section);

            // Validate configuration
            var validationResults = ValidateConfiguration(configuration);
            if (validationResults.Any(r => r.ErrorMessage?.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) == true))
            {
                var errors = string.Join(Environment.NewLine, validationResults
                    .Where(r => r.ErrorMessage?.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) == true)
                    .Select(r => r.ErrorMessage));
                
                throw new ValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
            }

            // Load existing config
            var configJson = File.Exists(_configPath) 
                ? File.ReadAllText(_configPath) 
                : "{}";

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, _jsonOptions) 
                ?? new Dictionary<string, JsonElement>();

            // Update section with schema version
            var sectionElement = JsonSerializer.SerializeToElement(configuration, _jsonOptions);
            config[section] = sectionElement;
            config[$"{section}_SchemaVersion"] = JsonSerializer.SerializeToElement(GetSchemaVersion<T>());

            // Save back to file
            var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_configPath, updatedJson);

            // Update cache and run health check
            _cache[section] = configuration;
            RunHealthCheck(null);

            // Log any warnings
            foreach (var warning in validationResults.Where(r => !r.ErrorMessage?.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) == true))
            {
                _logger.LogWarning("Configuration warning: {Message}", warning.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for section {Section}", section);
            throw;
        }
    }

    /// <summary>
    /// Validates configuration and returns detailed validation results
    /// </summary>
    public IEnumerable<ValidationResult> ValidateConfiguration<T>(T configuration) where T : class
    {
        if (configuration == null)
        {
            return new[] { new ValidationResult("ERROR: Configuration cannot be null") };
        }

        var results = new List<ValidationResult>();
        var context = new ValidationContext(configuration);

        // Perform validation
        Validator.TryValidateObject(configuration, context, results, true);

        // Add schema version validation
        var schemaResults = ValidateSchemaVersion<T>();
        if (schemaResults != null)
        {
            results.Add(schemaResults);
        }

        return results;
    }

    /// <summary>
    /// Performs health check on all configuration sections
    /// </summary>
    private void RunHealthCheck(object? state)
    {
        try
        {
            _logger.LogDebug("Running configuration health check");

            foreach (var (section, config) in _cache)
            {
                var results = ValidateConfiguration(config);
                var errors = results.Where(r => r.ErrorMessage?.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) == true);
                var warnings = results.Where(r => !r.ErrorMessage?.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) == true);

                if (errors.Any())
                {
                    _logger.LogError("Configuration health check failed for section {Section}:{NewLine}{Errors}", 
                        section, Environment.NewLine, string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
                }

                if (warnings.Any())
                {
                    _logger.LogWarning("Configuration health check warnings for section {Section}:{NewLine}{Warnings}", 
                        section, Environment.NewLine, string.Join(Environment.NewLine, warnings.Select(w => w.ErrorMessage)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration health check");
        }
    }

    /// <summary>
    /// Loads configuration with environment-specific overrides
    /// </summary>
    private T LoadConfiguration<T>(string section) where T : class, new()
    {
        // Load base configuration
        var baseConfig = LoadBaseConfiguration<T>(section);

        // Apply environment overrides
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrEmpty(environment))
        {
            var overridePath = $"{Path.GetDirectoryName(_configPath)}/config.{environment}.json";
            if (File.Exists(overridePath))
            {
                try
                {
                    var overrideJson = File.ReadAllText(overridePath);
                    var overrides = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(overrideJson, _jsonOptions);
                    
                    if (overrides?.TryGetValue(section, out var overrideElement) == true)
                    {
                        var overrideConfig = overrideElement.Deserialize<T>(_jsonOptions);
                        if (overrideConfig != null)
                        {
                            // Merge override properties into base config
                            foreach (var prop in typeof(T).GetProperties())
                            {
                                var overrideValue = prop.GetValue(overrideConfig);
                                if (overrideValue != null)
                                {
                                    prop.SetValue(baseConfig, overrideValue);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading environment configuration overrides from {Path}", overridePath);
                }
            }
        }

        return baseConfig;
    }

    /// <summary>
    /// Loads base configuration from the main config file
    /// </summary>
    private T LoadBaseConfiguration<T>(string section) where T : class, new()
    {
        // Load configuration file
        var configJson = File.Exists(_configPath) 
            ? File.ReadAllText(_configPath) 
            : "{}";

        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, _jsonOptions) 
            ?? new Dictionary<string, JsonElement>();

        // Get section or create new
        if (config.TryGetValue(section, out var sectionElement))
        {
            return sectionElement.Deserialize<T>(_jsonOptions) ?? new T();
        }

        return new T();
    }

    /// <summary>
    /// Gets the schema version for a configuration type
    /// </summary>
    private static string GetSchemaVersion<T>() where T : class
    {
        var type = typeof(T);
        var schemaVersion = type.GetCustomAttribute<SchemaVersionAttribute>()?.Version ?? "1.0";
        return schemaVersion;
    }

    /// <summary>
    /// Validates the schema version of the configuration
    /// </summary>
    private ValidationResult? ValidateSchemaVersion<T>() where T : class
    {
        var configVersion = GetSchemaVersion<T>();
        var currentVersion = typeof(T).GetCustomAttribute<SchemaVersionAttribute>()?.Version;

        if (currentVersion != null && Version.Parse(configVersion) < Version.Parse(currentVersion))
        {
            return new ValidationResult($"WARNING: Configuration schema version {configVersion} is older than current version {currentVersion}");
        }

        return null;
    }
}

/// <summary>
/// Defines the schema version for a configuration class
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class SchemaVersionAttribute : Attribute
{
    public string Version { get; }

    public SchemaVersionAttribute(string version)
    {
        Version = version;
    }
}

/// <summary>
/// Exception thrown for configuration-related errors
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message, Exception? innerException = null) 
        : base(message, innerException)
    {
    }
}