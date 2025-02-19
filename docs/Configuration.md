# Configuration Validation System

The CodeBuddy configuration system provides robust validation and versioning capabilities to ensure reliable configuration management across different environments and versions.

## Configuration Schema Versioning

Configurations are versioned using the `SchemaVersionAttribute`:

```csharp
[SchemaVersion("2.0")]
public class ValidationConfiguration : BaseConfiguration
{
    // Configuration properties
}
```

## Validation Rules

### Built-in Validation Attributes

- `Required` - Marks a property as required
- `Range` - Specifies numeric range constraints
- `MinLength` - Specifies minimum length for collections
- `EnvironmentSpecific` - Restricts values to specific environments
- `SensitiveData` - Marks properties containing sensitive data
- `Reloadable` - Indicates properties that can be modified at runtime
- `RequiresBackup` - Requires backup before modification
- `ValidEnumValues` - Validates enum values

Example:
```csharp
public class ValidationConfiguration : BaseConfiguration
{
    [Required]
    [Range(1, 10)]
    public int MaxConcurrentValidations { get; set; }

    [EnvironmentSpecific("Development", "Staging", "Production")]
    public Dictionary<string, int> ResourceLimits { get; set; }

    [SensitiveData]
    public string? ValidationApiKey { get; set; }
}
```

## Configuration Migration

### Version Migration

When configuration schema changes, create a migration class:

```csharp
public class ValidationConfigV1ToV2Migration : IConfigurationMigration
{
    public string FromVersion => "1.0";
    public string ToVersion => "2.0";

    public object Migrate(object configuration)
    {
        var v1Config = (ValidationConfiguration)configuration;
        // Perform migration logic
        return v1Config;
    }

    public ValidationResult Validate(object configuration)
    {
        // Validate configuration before migration
        return ValidationResult.Success();
    }
}
```

Register the migration:
```csharp
var migrationManager = new ConfigurationMigrationManager();
migrationManager.RegisterMigration<ValidationConfiguration>(new ValidationConfigV1ToV2Migration());
```

### Backup and Rollback

Configurations are automatically backed up before migrations:
- Backups stored in `config/backups` directory
- Naming format: `{ConfigName}_v{Version}_{Timestamp}.json`
- Automatic rollback on migration failure

## Configuration Health Checks

The `SystemHealthDashboard` monitors configuration health:
- Schema version validation
- Configuration integrity checks
- Migration history tracking
- Environment-specific validation
- Resource limit monitoring

## Plugin Developer Guidelines

### Creating Plugin Configurations

1. Inherit from `BaseConfiguration`
2. Use schema versioning
3. Apply validation attributes
4. Implement custom validation logic

Example:
```csharp
[SchemaVersion("1.0")]
public class PluginConfiguration : BaseConfiguration
{
    [Required]
    public string PluginName { get; set; }

    [EnvironmentSpecific("Development", "Production")]
    public Dictionary<string, string> Settings { get; set; }

    public override ValidationResult? Validate()
    {
        var baseResult = base.Validate();
        if (baseResult?.ValidationResult != ValidationResult.Success)
        {
            return baseResult;
        }

        // Custom validation logic
        return ValidationResult.Success;
    }
}
```

### Configuration Migration

1. Create migration class implementing `IConfigurationMigration`
2. Register migration with `ConfigurationMigrationManager`
3. Test migration paths
4. Document breaking changes

## Best Practices

1. Always version configurations
2. Use appropriate validation attributes
3. Implement custom validation when needed
4. Create migrations for breaking changes
5. Test configurations across environments
6. Document configuration requirements
7. Monitor configuration health
8. Backup sensitive configurations
9. Use environment-specific validation
10. Handle migration failures gracefully