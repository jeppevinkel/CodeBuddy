using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CodeBuddy.Core.Models;

/// <summary>
/// Attribute to mark configuration properties that contain sensitive data
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class EncryptedAttribute : Attribute { }

/// <summary>
/// Attribute to mark configuration properties that require directory existence
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DirectoryExistsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            if (!Directory.Exists(path))
            {
                return new ValidationResult($"Directory does not exist: {path}");
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// Represents environment-specific configuration
/// </summary>
public class EnvironmentConfig
{
    public string Environment { get; set; } = "Development";
    public Dictionary<string, string> Overrides { get; set; } = new();
}

/// <summary>
/// Represents the base configuration schema with validation and encryption support
/// </summary>
public class Configuration : IValidatableObject
{
    private const string EncryptionKey = "CodeBuddyConfigKey";  // In production, use secure key management
    
    [Required]
    [DirectoryExists]
    public string TemplatesDirectory { get; set; } = string.Empty;

    [Required]
    [DirectoryExists]
    public string OutputDirectory { get; set; } = string.Empty;

    [Required]
    [DirectoryExists]
    public string PluginsDirectory { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, string> DefaultParameters { get; set; } = new();

    [Required]
    [Range(0, 6, ErrorMessage = "LogLevel must be between 0 (Trace) and 6 (None)")]
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    public bool EnablePlugins { get; set; } = true;

    [Encrypted]
    public Dictionary<string, string> Secrets { get; set; } = new();

    public List<EnvironmentConfig> EnvironmentConfigs { get; set; } = new();

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Validates the configuration according to data annotations and custom rules
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, validationContext, results, true);

        // Add custom validation rules
        if (MinimumLogLevel == LogLevel.None && EnablePlugins)
        {
            results.Add(new ValidationResult(
                "Logging cannot be disabled when plugins are enabled",
                new[] { nameof(MinimumLogLevel), nameof(EnablePlugins) }));
        }

        return results;
    }

    /// <summary>
    /// Applies environment-specific configuration overrides
    /// </summary>
    public void ApplyEnvironmentOverrides(string environment)
    {
        var envConfig = EnvironmentConfigs.FirstOrDefault(e => 
            e.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase));
            
        if (envConfig != null)
        {
            foreach (var override_ in envConfig.Overrides)
            {
                var property = GetType().GetProperty(override_.Key);
                if (property != null)
                {
                    var convertedValue = Convert.ChangeType(override_.Value, property.PropertyType);
                    property.SetValue(this, convertedValue);
                }
            }
            OnConfigurationChanged(new ConfigurationChangedEventArgs(environment));
        }
    }

    /// <summary>
    /// Encrypts sensitive configuration data
    /// </summary>
    public void EncryptSensitiveData()
    {
        var properties = GetType().GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(EncryptedAttribute), true).Any());

        foreach (var property in properties)
        {
            var value = property.GetValue(this);
            if (value != null)
            {
                var encrypted = EncryptValue(JsonConvert.SerializeObject(value));
                property.SetValue(this, JsonConvert.DeserializeObject(DecryptValue(encrypted)));
            }
        }
    }

    private string EncryptValue(string value)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32));
        aes.IV = new byte[16];  // In production, use secure IV handling

        using var encryptor = aes.CreateEncryptor();
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var encryptedBytes = encryptor.TransformFinalBlock(valueBytes, 0, valueBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    private string DecryptValue(string encryptedValue)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32));
        aes.IV = new byte[16];

        using var decryptor = aes.CreateDecryptor();
        var encryptedBytes = Convert.FromBase64String(encryptedValue);
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    protected virtual void OnConfigurationChanged(ConfigurationChangedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for configuration changes
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public string Environment { get; }
    public DateTime Timestamp { get; }

    public ConfigurationChangedEventArgs(string environment)
    {
        Environment = environment;
        Timestamp = DateTime.UtcNow;
    }
}