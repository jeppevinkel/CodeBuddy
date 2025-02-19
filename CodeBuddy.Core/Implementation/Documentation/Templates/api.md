# {{Name}}

**Namespace:** {{Namespace}}

{{Description}}

## Overview

{{Overview}}

## Installation

```bash
dotnet add package {{PackageName}}
```

## Usage

```csharp
{{UsageExample}}
```

## API Reference

### Properties

| Name | Type | Description |
|------|------|-------------|
{{#Properties}}
| {{Name}} | {{Type}} | {{Description}} |
{{/Properties}}

### Methods

{{#Methods}}
#### {{Name}}

{{Description}}

**Parameters:**
{{#Parameters}}
- `{{Name}}` ({{Type}}): {{Description}}
{{/Parameters}}

**Returns:** {{ReturnType}}

**Example:**
```csharp
{{Example}}
```

{{/Methods}}

## Configuration

```json
{
    {{ConfigurationExample}}
}
```

## Examples

{{#Examples}}
### {{Title}}

{{Description}}

```csharp
{{Code}}
```

{{/Examples}}

## See Also

{{#SeeAlso}}
- [{{Text}}]({{Link}})
{{/SeeAlso}}