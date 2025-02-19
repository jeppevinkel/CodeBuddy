# {{PluginName}}

{{Description}}

## Overview

{{Overview}}

## Features

{{#each Features}}
- {{Name}}: {{Description}}
{{/each}}

## Installation

```bash
dotnet add package {{PackageName}}
```

## Configuration

```json
{{ConfigurationExample}}
```

### Configuration Options

| Option | Type | Required | Default | Description |
|--------|------|-----------|---------|-------------|
{{#each ConfigurationOptions}}
| {{Name}} | {{Type}} | {{Required}} | {{Default}} | {{Description}} |
{{/each}}

## Usage

### Basic Usage

```csharp
{{BasicUsageExample}}
```

### Advanced Scenarios

{{#each AdvancedScenarios}}
#### {{Name}}

{{Description}}

```csharp
{{Example}}
```
{{/each}}

## API Reference

{{#each Types}}
### {{Name}}

{{Description}}

#### Methods

{{#each Methods}}
##### {{Name}}

{{Description}}

Parameters:
{{#each Parameters}}
- `{{Name}}` ({{Type}}): {{Description}}
{{/each}}

Returns: {{ReturnType}}
{{/each}}
{{/each}}

## Examples

{{#each Examples}}
### {{Name}}

{{Description}}

```csharp
{{Code}}
```
{{/each}}

## Troubleshooting

{{#each TroubleshootingItems}}
### {{Problem}}

{{Solution}}
{{/each}}

## Best Practices

{{BestPractices}}

## Version History

{{#each Versions}}
### {{Version}} ({{Date}})

{{#each Changes}}
- {{this}}
{{/each}}
{{/each}}