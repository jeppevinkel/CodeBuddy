# Getting Started with CodeBuddy

Welcome to CodeBuddy! This guide will help you get started with integrating CodeBuddy into your development workflow.

## Installation

1. Install the CodeBuddy NuGet package:

```bash
dotnet add package CodeBuddy
```

2. For CI/CD integration, install the CLI tool:

```bash
dotnet tool install --global CodeBuddy.CLI
```

## Basic Configuration

1. Create a `codebuddy.json` configuration file in your project root:

```json
{
  "validation": {
    "enabled": true,
    "minCoverage": 80,
    "analyzers": ["csharp", "typescript", "python"]
  },
  "documentation": {
    "generateApi": true,
    "generateDiagrams": true,
    "outputPath": "./docs"
  },
  "plugins": {
    "enabled": true,
    "directory": "./plugins"
  }
}
```

2. Initialize CodeBuddy in your project:

```csharp
using CodeBuddy.Core;

var configuration = Configuration.LoadFromFile("codebuddy.json");
var validator = new CodeValidator(configuration);
```

## Basic Usage

### Code Validation

```csharp
// Validate a code file
var result = await validator.ValidateFileAsync("path/to/file.cs");
if (!result.IsValid)
{
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"{issue.Severity}: {issue.Message}");
    }
}
```

### Documentation Generation

```csharp
// Generate documentation
var docGenerator = new DocumentationGenerator(configuration);
await docGenerator.GenerateApiDocumentationAsync();
```

### Plugin Development

1. Create a new class library project
2. Implement the IPlugin interface:

```csharp
public class CustomPlugin : IPlugin
{
    public string Name => "CustomPlugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginContext context)
    {
        // Plugin initialization code
        return Task.CompletedTask;
    }
}
```

## CLI Usage

Generate documentation:
```bash
codebuddy generate-docs --include-typescript --validate
```

Validate code:
```bash
codebuddy validate --path ./src
```

## Advanced Topics

- [API Documentation](api/overview.md)
- [Plugin Development Guide](plugins/overview.md)
- [Validation Pipeline](validation/overview.md)
- [Architecture Documentation](architecture/overview.md)
- [Configuration Guide](configuration/overview.md)

## Troubleshooting

### Common Issues

1. **Documentation Generation Fails**
   - Ensure XML documentation is enabled in project
   - Check file permissions in output directory
   - Verify PlantUML is installed for diagram generation

2. **Plugin Loading Issues**
   - Check plugin compatibility version
   - Verify plugin directory permissions
   - Enable debug logging for detailed diagnostics

3. **Validation Errors**
   - Review validation rules configuration
   - Check language analyzer installation
   - Verify file encoding and format

## Next Steps

- Explore the [example projects](examples/)
- Join our [community forum](https://community.codebuddy.com)
- Contribute to [CodeBuddy on GitHub](https://github.com/codebuddy)

## Support

For additional help:
- Check our [FAQ](docs/faq.md)
- Report issues on [GitHub](https://github.com/codebuddy/issues)
- Join our [Discord community](https://discord.gg/codebuddy)