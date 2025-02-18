# {{name}}

**Namespace:** {{namespace}}

{{description}}

## Type Information
- **Assembly:** {{assembly}}
- **Base Type:** {{baseType}}
{{#if interfaces}}
- **Implements:**
{{#each interfaces}}
  - {{this}}
{{/each}}
{{/if}}

## Properties
{{#if properties}}
| Name | Type | Description |
|------|------|-------------|
{{#each properties}}
| {{name}} | {{type}} | {{description}} |
{{/each}}
{{else}}
This type has no public properties.
{{/if}}

## Methods
{{#if methods}}
{{#each methods}}
### {{name}}
{{description}}

#### Parameters
{{#if parameters}}
| Name | Type | Description |
|------|------|-------------|
{{#each parameters}}
| {{name}} | {{type}} | {{description}} |
{{/each}}
{{else}}
This method takes no parameters.
{{/if}}

#### Returns
{{returnType}}

{{#if examples}}
#### Examples
{{#each examples}}
```{{language}}
{{code}}
```
{{/each}}
{{/if}}

{{/each}}
{{else}}
This type has no public methods.
{{/if}}

## Usage Notes
{{usageNotes}}