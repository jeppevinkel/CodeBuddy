# Plugin Development Guide

This guide covers the development of plugins for the CodeBuddy system.

## Configuration Validation

Plugins must implement proper configuration validation to ensure reliable operation. The CodeBuddy configuration system provides several tools for this purpose.

### Basic Configuration Setup

```csharp
[SchemaVersion("1.0")]
public class MyPluginConfiguration : BaseConfiguration
{
    [Required]
    public string PluginName { get; set; }

    [Range(1, 100)]
    public int MaxThreads { get; set; } = 10;

    [EnvironmentSpecific("Development", "Production")]
    public Dictionary<string, string> Settings { get; set; } = new();

    [SensitiveData]
    public string? ApiKey { get; set; }

    public override ValidationResult? Validate()
    {
        var baseResult = base.Validate();
        if (baseResult?.ValidationResult != ValidationResult.Success)
        {
            return baseResult;
        }

        // Custom validation logic
        if (MaxThreads > Environment.ProcessorCount)
        {
            return new ValidationResult(
                $"MaxThreads ({MaxThreads}) cannot exceed system processor count ({Environment.ProcessorCount})");
        }

        return ValidationResult.Success;
    }
}
```

### Configuration Migration

When updating your plugin's configuration schema, implement a migration:

```csharp
public class MyPluginConfigV1ToV2Migration : IConfigurationMigration
{
    public string FromVersion => "1.0";
    public string ToVersion => "2.0";

    public object Migrate(object configuration)
    {
        var v1Config = (MyPluginConfiguration)configuration;
        
        // Modify configuration for v2 schema
        v1Config.MaxThreads = Math.Min(v1Config.MaxThreads, 50); // New limit in v2
        
        return v1Config;
    }

    public ValidationResult Validate(object configuration)
    {
        var config = (MyPluginConfiguration)configuration;
        
        if (config.MaxThreads > 100)
        {
            return ValidationResult.Failed(
                "Cannot migrate configurations with MaxThreads > 100");
        }
        
        return ValidationResult.Success();
    }
}
```

Register the migration:

```csharp
public class MyPlugin : IPlugin
{
    public void Initialize(IPluginContext context)
    {
        // Register configuration migration
        context.ConfigurationManager
            .RegisterMigration<MyPluginConfiguration>(new MyPluginConfigV1ToV2Migration());
            
        // Load and validate configuration
        var config = context.ConfigurationManager
            .LoadConfiguration<MyPluginConfiguration>();
            
        if (context.ConfigurationManager.RequiresMigration(config))
        {
            config = await context.ConfigurationManager.MigrateAsync(config);
        }
    }
}
```

### Configuration Recovery System

The system includes an automatic configuration recovery system that helps prevent and recover from configuration-related failures:

#### Features
- Automatic configuration backups
- Intelligent recovery from failures
- Version-aware recovery process
- Health monitoring integration

#### Best Practices for Recovery-Aware Plugins

1. Use the `[RequiresBackup]` attribute for critical settings:
```csharp
public class MyPluginConfiguration : BaseConfiguration
{
    [Required]
    [RequiresBackup]
    public Dictionary<string, string> CriticalSettings { get; set; }
}
```

2. Implement graceful degradation:
```csharp
public class MyPlugin : IPlugin
{
    public async Task OnConfigurationChanged(MyPluginConfiguration config)
    {
        try
        {
            // Apply new configuration
            await ApplyConfiguration(config);
        }
        catch (ConfigurationException ex)
        {
            // Let the recovery system handle it
            throw;
        }
    }
}
```

3. Monitor recovery metrics:
```csharp
public class MyPlugin : IPlugin
{
    public void Initialize(IPluginContext context)
    {
        // Subscribe to recovery events
        context.ErrorRecoveryOrchestrator.OnRecoveryAttempted += (sender, args) =>
        {
            if (args.PluginId == Id)
            {
                // Log recovery attempt
                _logger.LogInformation($"Configuration recovery attempted: {args.Success}");
            }
        };
    }
}
```

### Configuration Validation Best Practices

1. Always inherit from `BaseConfiguration`
2. Use appropriate validation attributes
3. Implement custom validation logic
4. Handle environment-specific settings
5. Protect sensitive data
6. Create migrations for breaking changes
7. Test configuration validation
8. Document configuration requirements
9. Mark critical settings with `[RequiresBackup]`
10. Implement graceful configuration failure handling

## Plugin Structure

TBD: Add information about plugin structure, lifecycle, and dependencies

## Plugin API

TBD: Document plugin API interfaces and usage

## Testing Plugins

TBD: Add plugin testing guidelines and examples

## Deployment

TBD: Document plugin packaging and deployment