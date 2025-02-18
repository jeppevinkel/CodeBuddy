# {{title}}

**Namespace:** {{namespace}}

{{description}}

## Installation

```bash
dotnet add package CodeBuddy
```

## Usage

```csharp
{{usage}}
```

## API Reference

### Properties

| Name | Type | Description | Default |
|------|------|-------------|---------|
{{#properties}}
| {{name}} | {{type}} | {{description}} | {{default}} |
{{/properties}}

### Methods

{{#methods}}
#### {{name}}

```csharp
{{signature}}
```

{{description}}

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
{{#parameters}}
| {{name}} | {{type}} | {{description}} |
{{/parameters}}

**Returns:**

{{returnType}} - {{returnDescription}}

**Example:**

```csharp
{{example}}
```

{{/methods}}

## Examples

{{#examples}}
### {{title}}

{{description}}

```csharp
{{code}}
```

{{/examples}}

## Best Practices

{{#bestPractices}}
* {{description}}
{{/bestPractices}}

## Common Issues

{{#issues}}
### {{title}}

{{description}}

**Solution:**

{{solution}}
{{/issues}}

## Related Documentation

{{#related}}
* [{{title}}]({{link}})
{{/related}}