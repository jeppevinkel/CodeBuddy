# {{ApiName}}

{{Description}}

## Overview

{{Overview}}

## Installation

```bash
dotnet add package CodeBuddy
```

## Basic Usage

```csharp
{{BasicUsageExample}}
```

## API Reference

{{#each Types}}
### {{Name}}

{{Description}}

#### Properties

| Name | Type | Description |
|------|------|-------------|
{{#each Properties}}
| {{Name}} | {{Type}} | {{Description}} |
{{/each}}

#### Methods

{{#each Methods}}
##### {{Name}}

{{Description}}

Parameters:
{{#each Parameters}}
- `{{Name}}` ({{Type}}): {{Description}}
{{/each}}

Returns: {{ReturnType}}

Example:
```csharp
{{Example}}
```
{{/each}}

{{/each}}

## Advanced Usage

{{AdvancedUsage}}

## Best Practices

{{BestPractices}}

## Common Issues and Solutions

{{Troubleshooting}}

## Related Resources

{{RelatedResources}}