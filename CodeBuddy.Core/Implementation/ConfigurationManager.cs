using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Implementation.Configuration;
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
    private readonly System.Threading.Timer _healthCheckTimer;
    private readonly IConfigurationMigrationManager _migrationManager;
    private readonly ConfigurationValidationManager _validationManager;

    public ConfigurationManager(
        IFileOperations fileOperations, 
        ILogger<ConfigurationManager> logger,
        IServiceProvider serviceProvider,
        string configPath = "config.json")
    {
        _fileOperations = fileOperations;
        _logger = logger;
        _configPath = configPath;
        _migrationManager = new ConfigurationMigrationManager(logger);
        _validationManager = new ConfigurationValidationManager(serviceProvider);
        _secureStorage = new SecureConfigurationStorage(logger);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Initialize health check timer (runs every 5 minutes)
        _healthCheckTimer = new System.Threading.Timer(RunHealthCheck, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

        // Initialize file watcher for hot reload
        var configDir = Path.GetDirectoryName(_configPath) ?? ".";
        _configWatcher = new FileSystemWatcher(configDir, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _configWatcher.Changed += HandleConfigurationFileChange;
    }

    private readonly Dictionary<string, List<Delegate>> _changeCallbacks = new();
    private readonly SecureConfigurationStorage _secureStorage;
    private readonly FileSystemWatcher _configWatcher;

    /// <summary>
    /// Gets configuration for a specific section with validation, caching, and migration
    /// </summary>
    public async Task<T> GetConfiguration<T>(string section) where T : class, new()
    {
        try
        {
            _logger.LogDebug("Getting configuration for section {Section}", section);

            // Register configuration type
            _validationManager.RegisterConfiguration<T>();

            // Check cache first
            if (_cache.TryGetValue(section, out var cached))
            {
                return (T)cached;
            }

            // Load configuration
            var config = LoadConfiguration<T>(section);
            
            // Check if migration is needed
            if (_migrationManager.NeedsMigration(section, config))
            {
                _logger.LogInformation("Configuration migration needed for section {Section}", section);
                
                var migrationResult = await _migrationManager.MigrateConfiguration(section, config);
                if (!migrationResult.Success)
                {
                    _logger.LogError("Configuration migration failed for section {Section}: {Error}", 
                        section, migrationResult.Error);
                        
                    // Use original config if migration failed
                    if (migrationResult.Configuration == null)
                    {
                        _logger.LogWarning("Using original configuration for section {Section}", section);
                    }
                    else
                    {
                        config = (T)migrationResult.Configuration;
                    }
                }
                else
                {
                    config = (T)migrationResult.Configuration!;
                    _logger.LogInformation("Configuration migration successful for section {Section}", section);
                }
            }
            
            // Validate configuration using the validation framework
            var validationResult = await _validationManager.ValidateAsync(config);
            if (!validationResult.IsValid)
            {
                if (validationResult.Severity == ValidationSeverity.Error)
                {
                    throw new ValidationException($"Configuration validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, validationResult.Errors)}");
                }
                else
                {
                    foreach (var warning in validationResult.Errors)
                    {
                        _logger.LogWarning("Configuration warning for section {Section}: {Warning}", section, warning);
                    }
                }
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
    /// Saves configuration with validation, schema checks, and versioning
    /// </summary>
    public async Task<T> GetConfiguration<T>(string section, string environment) where T : class, new()
    {
        var baseConfig = await GetConfiguration<T>(section);
        
        if (!string.IsNullOrEmpty(environment))
        {
            var overridePath = $"{Path.GetDirectoryName(_configPath)}/config.{environment}.json";
            if (File.Exists(overridePath))
            {
                try
                {
                    var overrideJson = await File.ReadAllTextAsync(overridePath);
                    var overrides = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(overrideJson, _jsonOptions);
                    
                    if (overrides?.TryGetValue(section, out var overrideElement) == true)
                    {
                        var overrideConfig = overrideElement.Deserialize<T>(_jsonOptions);
                        if (overrideConfig != null)
                        {
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
                    _logger.LogWarning(ex, "Error loading environment configuration overrides from {Environment}", environment);
                }
            }
        }

        // Apply command line overrides
        ApplyCommandLineOverrides(baseConfig);

        return baseConfig;
    }

    public async Task SaveConfiguration<T>(string section, T configuration) where T : class
    {
        try
        {
            _logger.LogInformation("Saving configuration for section {Section}", section);

            // Register configuration type
            _validationManager.RegisterConfiguration<T>();

            // Validate configuration using the validation framework
            var validationResult = await _validationManager.ValidateAsync(configuration);
            if (!validationResult.IsValid && validationResult.Severity == ValidationSeverity.Error)
            {
                throw new ValidationException(
                    $"Configuration validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, validationResult.Errors)}");
            }

            // Load existing config
            var configJson = File.Exists(_configPath) 
                ? await File.ReadAllTextAsync(_configPath)
                : "{}";

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, _jsonOptions) 
                ?? new Dictionary<string, JsonElement>();

            // Create backup before saving
            var backupDir = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "backups");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, $"{Path.GetFileNameWithoutExtension(_configPath)}_{DateTime.Now:yyyyMMddHHmmss}.json");
            if (File.Exists(_configPath))
            {
                await File.CopyAsync(_configPath, backupPath);
            }

            try
            {
                // Update section with schema version
                var sectionElement = JsonSerializer.SerializeToElement(configuration, _jsonOptions);
                config[section] = sectionElement;
                config[$"{section}_SchemaVersion"] = JsonSerializer.SerializeToElement(GetSchemaVersion<T>());

                // Save back to file
                var updatedJson = JsonSerializer.Serialize(config, _jsonOptions);
                await File.WriteAllTextAsync(_configPath, updatedJson);
                
                _logger.LogInformation("Configuration saved successfully for section {Section} with backup at {BackupPath}", 
                    section, backupPath);
            }
            catch (Exception)
            {
                _logger.LogWarning("Error saving configuration, attempting to restore from backup {BackupPath}", backupPath);
                if (File.Exists(backupPath))
                {
                    await File.CopyAsync(backupPath, _configPath, true);
                }
                throw;
            }

            // Update cache and run health check
            _cache[section] = configuration;
            RunHealthCheck(null);

            // Log any warnings from validation
            if (!validationResult.IsValid && validationResult.Severity == ValidationSeverity.Warning)
            {
                foreach (var warning in validationResult.Errors)
                {
                    _logger.LogWarning("Configuration warning: {Message}", warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for section {Section}", section);
            throw;
        }
    }

    public IEnumerable<ValidationResult> ValidateConfiguration<T>(T configuration) where T : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(configuration);
        Validator.TryValidateObject(configuration, validationContext, validationResults, true);
        return validationResults;
    }

    public void RegisterChangeCallback<T>(string section, Action<T> callback) where T : class
    {
        if (!_changeCallbacks.ContainsKey(section))
        {
            _changeCallbacks[section] = new List<Delegate>();
        }
        _changeCallbacks[section].Add(callback);
    }

    public async Task<string> GetSecureValue(string section, string key)
    {
        return await _secureStorage.GetSecureValue($"{section}:{key}");
    }

    public async Task SetSecureValue(string section, string key, string value)
    {
        await _secureStorage.SetSecureValue($"{section}:{key}", value);
    }

    public async Task BackupConfiguration(string backupPath)
    {
        if (File.Exists(_configPath))
        {
            await File.CopyAsync(_configPath, backupPath);
            _logger.LogInformation("Configuration backed up to {BackupPath}", backupPath);
        }
    }

    public async Task RestoreConfiguration(string backupPath)
    {
        if (File.Exists(backupPath))
        {
            await File.CopyAsync(backupPath, _configPath, true);
            _logger.LogInformation("Configuration restored from {BackupPath}", backupPath);
            
            // Clear cache and notify subscribers
            _cache.Clear();
            foreach (var section in _changeCallbacks.Keys)
            {
                await NotifyConfigurationChanged(section);
            }
        }
    }

    public async Task<IDictionary<string, string>> GetConfigurationMetadata(string section)
    {
        var metadata = new Dictionary<string, string>
        {
            ["SchemaVersion"] = GetConfigurationVersion(section),
            ["LastModified"] = File.GetLastWriteTime(_configPath).ToString("O"),
            ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };

        return metadata;
    }

    public async Task<string> GenerateConfigurationDocumentation()
    {
        var documentation = new StringBuilder();
        documentation.AppendLine("# Configuration Documentation");
        documentation.AppendLine();

        foreach (var (section, config) in _cache)
        {
            documentation.AppendLine($"## {section}");
            documentation.AppendLine($"Schema Version: {GetConfigurationVersion(section)}");
            documentation.AppendLine();

            var type = config.GetType();
            foreach (var prop in type.GetProperties())
            {
                documentation.AppendLine($"### {prop.Name}");
                
                var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    documentation.AppendLine(description);
                }

                var validationAttributes = prop.GetCustomAttributes().OfType<ValidationAttribute>();
                if (validationAttributes.Any())
                {
                    documentation.AppendLine("\nValidation:");
                    foreach (var attr in validationAttributes)
                    {
                        documentation.AppendLine($"- {attr.GetType().Name}: {attr.ErrorMessage}");
                    }
                }

                documentation.AppendLine();
            }
        }

        return documentation.ToString();
    }

    private void HandleConfigurationFileChange(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid changes
        Task.Delay(500).ContinueWith(async _ =>
        {
            try
            {
                _logger.LogInformation("Configuration file changed: {Path}", e.FullPath);
                
                // Clear cache for changed sections
                var changedSections = await GetChangedSections(e.FullPath);
                foreach (var section in changedSections)
                {
                    _cache.Remove(section);
                    await NotifyConfigurationChanged(section);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling configuration file change");
            }
        });
    }

    private async Task<IEnumerable<string>> GetChangedSections(string path)
    {
        try
        {
            var previousConfig = _cache.Keys.ToList();
            var newConfigJson = await File.ReadAllTextAsync(path);
            var newConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(newConfigJson, _jsonOptions);

            return newConfig?.Keys.Where(k => !k.EndsWith("_SchemaVersion")) ?? Enumerable.Empty<string>();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private async Task NotifyConfigurationChanged(string section)
    {
        if (_changeCallbacks.TryGetValue(section, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    var type = callback.GetType().GetGenericArguments()[0];
                    var config = await GetConfiguration(type, section);
                    callback.DynamicInvoke(config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying configuration change for section {Section}", section);
                }
            }
        }
    }

    private void ApplyCommandLineOverrides<T>(T config) where T : class
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var prop in typeof(T).GetProperties())
        {
            var overrideAttr = prop.GetCustomAttribute<CommandLineOverrideAttribute>();
            if (overrideAttr != null)
            {
                var argIndex = Array.FindIndex(args, a => a.Equals($"--{overrideAttr.ArgumentName}"));
                if (argIndex >= 0 && argIndex < args.Length - 1)
                {
                    var value = args[argIndex + 1];
                    var convertedValue = Convert.ChangeType(value, prop.PropertyType);
                    prop.SetValue(config, convertedValue);
                }
                else if (overrideAttr.Required)
                {
                    throw new ConfigurationException($"Required command line argument '--{overrideAttr.ArgumentName}' not provided");
                }
            }
        }
    }

    /// <summary>
    /// Performs health check on all configuration sections
    /// </summary>
    private async void RunHealthCheck(object? state)
    {
        try
        {
            _logger.LogDebug("Running configuration health check");

            foreach (var (section, config) in _cache)
            {
                var validationResult = await _validationManager.ValidateAsync(config);
                if (!validationResult.IsValid)
                {
                    var severity = validationResult.Severity == ValidationSeverity.Error ? "ERROR" : "WARNING";
                    _logger.LogWarning("Configuration health check {Severity} for section {Section}:{NewLine}{Errors}", 
                        severity, section, Environment.NewLine, string.Join(Environment.NewLine, validationResult.Errors));
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
        var configSection = type.GetCustomAttribute<ConfigurationSectionAttribute>();
        return configSection?.Version.ToString() ?? "1.0";
    }

    /// <summary>
    /// Gets the schema version for a configuration section
    /// </summary>
    public string GetConfigurationVersion(string section)
    {
        try
        {
            var configJson = File.Exists(_configPath) 
                ? File.ReadAllText(_configPath) 
                : "{}";

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, _jsonOptions) 
                ?? new Dictionary<string, JsonElement>();

            return config.TryGetValue($"{section}_SchemaVersion", out var version)
                ? version.GetString() ?? "1.0"
                : "1.0";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration version for section {Section}", section);
            return "1.0";
        }
    }

    /// <summary>
    /// Validates the schema version of the configuration
    /// </summary>
    private ValidationResult? ValidateSchemaVersion<T>() where T : class
    {
        var configVersion = GetSchemaVersion<T>();
        var currentVersion = typeof(T).GetCustomAttribute<ConfigurationSectionAttribute>()?.Version.ToString();

        if (currentVersion != null && Version.Parse(configVersion) < Version.Parse(currentVersion))
        {
            return new ValidationResult($"WARNING: Configuration schema version {configVersion} is older than current version {currentVersion}");
        }

        return null;
    }
}