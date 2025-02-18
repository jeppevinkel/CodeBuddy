# Configuration System

CodeBuddy uses a robust configuration system that provides schema-based validation, type safety, and version control for all configuration sections.

## Key Features

1. **Schema-based Validation**
   - Each configuration section has a defined schema with validation rules
   - Supports required fields, ranges, patterns, and custom validation
   - Automatic validation on load and save

2. **Type-safe Configuration**
   - Strongly-typed configuration classes
   - Compile-time type checking
   - IntelliSense support in editors

3. **Validation Framework**
   - Attribute-based validation rules
   - Custom validation logic support
   - Hierarchical validation (base + derived)
   - Severity levels (Error, Warning, Information)

4. **Version Control**
   - Schema versioning for configuration sections
   - Migration paths for configuration updates
   - Backward compatibility support
   - Configuration backups

5. **Environment Support**
   - Environment-specific overrides
   - Development/Staging/Production configurations
   - Secure sensitive settings

6. **Health Monitoring**
   - Periodic configuration validation
   - Validation error reporting
   - Configuration change tracking
   - Performance monitoring

## Usage Example

```csharp
// Define a configuration section
[ConfigurationSection("MyFeature", "Configuration for MyFeature", version: 1)]
public class MyFeatureConfiguration : BaseConfiguration
{
    [ConfigurationItem("Maximum items allowed", required: true, defaultValue: "100")]
    [RangeValidation(1, 1000)]
    public int MaxItems { get; set; } = 100;

    [ConfigurationItem("API endpoint", required: true)]
    [PatternValidation(@"^https?://.*$", "Must be a valid HTTP/HTTPS URL")]
    public string ApiEndpoint { get; set; }

    public override IEnumerable<string> Validate()
    {
        var errors = new List<string>(base.Validate());
        
        // Custom validation logic
        if (MaxItems > 500 && !ApiEndpoint.StartsWith("https://"))
        {
            errors.Add("HTTPS is required for high-volume endpoints");
        }
        
        return errors;
    }
}

// Use the configuration
public class MyFeature
{
    private readonly MyFeatureConfiguration _config;
    
    public MyFeature(IConfigurationManager configManager)
    {
        _config = await configManager.GetConfiguration<MyFeatureConfiguration>("MyFeature");
    }
}
```

## Validation Attributes

1. **ConfigurationSectionAttribute**
   - Defines a configuration section
   - Specifies name, description, and version

2. **ConfigurationItemAttribute**
   - Defines a configuration item
   - Specifies description, required status, and default value

3. **RangeValidationAttribute**
   - Validates numeric ranges
   - Supports min/max values

4. **PatternValidationAttribute**
   - Validates string patterns
   - Uses regular expressions

5. **CustomValidationAttribute**
   - Implements custom validation logic
   - Full control over validation rules

## Best Practices

1. **Configuration Structure**
   - Keep configuration sections focused and cohesive
   - Use meaningful names and descriptions
   - Document all configuration options

2. **Validation Rules**
   - Use appropriate validation attributes
   - Implement custom validation when needed
   - Consider performance implications

3. **Versioning**
   - Increment version for breaking changes
   - Provide migration paths
   - Document changes

4. **Security**
   - Protect sensitive configuration
   - Use secure transport for remote configuration
   - Validate all inputs

5. **Monitoring**
   - Monitor configuration health
   - Set up alerts for validation failures
   - Track configuration changes

## Migration Guide

When updating configuration schemas:

1. Create a new version of the configuration class
2. Implement migration logic in ConfigurationMigrationManager
3. Test migration with sample configurations
4. Document changes and migration path
5. Deploy with backward compatibility

Example:
```csharp
public class ConfigurationV2Migration : IConfigurationMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;
    
    public object Migrate(object oldConfig)
    {
        var v1 = (MyFeatureConfigurationV1)oldConfig;
        return new MyFeatureConfigurationV2
        {
            MaxItems = v1.MaxItems,
            ApiEndpoint = v1.ApiEndpoint,
            // New properties with defaults
            RetryCount = 3,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
```