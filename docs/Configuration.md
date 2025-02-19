# Configuration Management System

The CodeBuddy Configuration Management System provides a robust and flexible way to manage application settings across different environments, with support for validation, versioning, and secure storage.

## Configuration File Format

Configuration files are stored as JSON in the `config` directory. Each configuration section has its own file named `{section}.json`.

### Basic Structure

```json
{
  "version": "1.0",
  "settings": {
    // Section-specific settings here
  }
}
```

### Schema Versioning

All configuration files include a version field to track schema changes and enable automatic migrations:

```json
{
  "version": "1.0",
  "settings": {
    // Settings for version 1.0
  }
}
```

When the schema changes, the version is incremented (e.g., to "2.0") and the ConfigurationMigrationManager handles automatic updates.

## Configuration Sources

The system supports multiple configuration sources in order of precedence:

1. Command Line Arguments (highest priority)
2. Environment Variables
3. Configuration Files
4. Default Values (lowest priority)

### Environment Variables

Environment variables can override file-based configuration using the following format:
```
CONFIG_{SECTION}_{KEY}=value
```

Example:
```bash
# Override logging level in the Logging section
CONFIG_LOGGING_LEVEL=Debug
```

### Secure Configuration

Sensitive values (passwords, API keys, etc.) are stored securely using the SecureConfigurationStorage:

```csharp
// Store secure value
await configManager.SetSecureValue("database", "password", "secret123");

// Retrieve secure value
string password = await configManager.GetSecureValue("database", "password");
```

## Configuration Sections

### Logging Configuration

```json
{
  "version": "1.0",
  "settings": {
    "level": "Information",
    "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
    "filePath": "logs/codebuddy.log",
    "rollingInterval": "Day",
    "retainedFileCountLimit": 31
  }
}
```

### Validation Configuration

```json
{
  "version": "1.0",
  "settings": {
    "parallelValidation": true,
    "maxConcurrentValidations": 4,
    "cacheValidationResults": true,
    "cacheDuration": "00:30:00",
    "rules": {
      "enabled": true,
      "customRulesPath": "rules"
    }
  }
}
```

### Plugin Configuration

```json
{
  "version": "1.0",
  "settings": {
    "pluginDirectory": "plugins",
    "autoLoadPlugins": true,
    "enableHotReload": true,
    "isolation": {
      "enabled": true,
      "maxMemoryMB": 512,
      "timeoutSeconds": 30
    }
  }
}
```

## Command Line Interface

The `codebuddy config` command provides CLI access to configuration management:

```bash
# Get configuration value
codebuddy config get --key logging.level

# Set configuration value
codebuddy config set --key logging.level --value Debug

# List all configuration
codebuddy config list

# Reset to defaults
codebuddy config reset
```

## Configuration Validation

Configuration values are validated using data annotations and custom validation rules:

```csharp
public class LoggingConfiguration
{
    [Required]
    public string Level { get; set; }

    [Required]
    [RegularExpression(@"^logs/.*\.log$")]
    public string FilePath { get; set; }

    [Range(1, 365)]
    public int RetainedFileCountLimit { get; set; }
}
```

## Configuration Migration

When configuration schemas change, migrations are automatically handled:

1. The system detects a version mismatch
2. ConfigurationMigrationManager loads the appropriate migration
3. Configuration is upgraded to the new schema
4. New configuration is validated before being applied

Example migration:
```csharp
public class ConfigMigration_1_0_to_2_0 : IConfigurationMigration
{
    public string FromVersion => "1.0";
    public string ToVersion => "2.0";

    public async Task<T> Migrate<T>(T config) where T : class
    {
        // Migration logic here
        return upgradedConfig;
    }
}
```

## Environment-Specific Configuration

Different environments (development, staging, production) can have their own configuration overrides:

```bash
# Set environment
export ENVIRONMENT=production

# Get production-specific configuration
await configManager.GetConfiguration<LoggingConfiguration>("logging", "production");
```

## Change Notifications

Register for configuration changes:

```csharp
configManager.RegisterChangeCallback<LoggingConfiguration>("logging", config =>
{
    // Handle configuration change
    UpdateLoggingSettings(config);
});
```

## Best Practices

1. **Security**:
   - Never commit sensitive values to source control
   - Use SecureConfigurationStorage for sensitive data
   - Restrict access to configuration files

2. **Validation**:
   - Always validate configuration at startup
   - Use data annotations for basic validation
   - Implement custom validation for complex rules

3. **Versioning**:
   - Always include version in configuration files
   - Implement migrations for schema changes
   - Test migrations thoroughly

4. **Documentation**:
   - Document all configuration options
   - Include examples for common scenarios
   - Keep documentation updated with schema changes

5. **Testing**:
   - Write tests for configuration validation
   - Test configuration migrations
   - Verify environment variable overrides