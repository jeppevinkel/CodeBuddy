using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements configuration management with JSON file backing
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly IFileOperations _fileOperations;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _configPath;
    private readonly Dictionary<string, object> _cache = new();

    public ConfigurationManager(
        IFileOperations fileOperations, 
        ILogger<ConfigurationManager> logger,
        string configPath = "config.json")
    {
        _fileOperations = fileOperations;
        _logger = logger;
        _configPath = configPath;
    }

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

            // Load configuration file
            var configJson = File.Exists(_configPath) 
                ? File.ReadAllText(_configPath) 
                : "{}";

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            // Get section or create new
            if (config.TryGetValue(section, out var sectionElement))
            {
                var result = sectionElement.Deserialize<T>() ?? new T();
                _cache[section] = result;
                return result;
            }

            var defaultConfig = new T();
            _cache[section] = defaultConfig;
            return defaultConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration for section {Section}", section);
            throw;
        }
    }

    public void SaveConfiguration<T>(string section, T configuration) where T : class
    {
        try
        {
            _logger.LogInformation("Saving configuration for section {Section}", section);

            if (!ValidateConfiguration(configuration))
            {
                throw new ValidationException("Configuration validation failed");
            }

            // Load existing config
            var configJson = File.Exists(_configPath) 
                ? File.ReadAllText(_configPath) 
                : "{}";

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            // Update section
            var sectionElement = JsonSerializer.SerializeToElement(configuration);
            config[section] = sectionElement;

            // Save back to file
            var updatedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, updatedJson);

            // Update cache
            _cache[section] = configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for section {Section}", section);
            throw;
        }
    }

    public bool ValidateConfiguration<T>(T configuration) where T : class
    {
        if (configuration == null)
        {
            return false;
        }

        var context = new ValidationContext(configuration);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(configuration, context, results, true))
        {
            foreach (var result in results)
            {
                _logger.LogWarning("Configuration validation error: {Message}", result.ErrorMessage);
            }
            return false;
        }

        return true;
    }
}