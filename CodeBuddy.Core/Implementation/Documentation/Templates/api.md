# {{name}}

**Namespace:** {{namespace}}

{{description}}

## Interfaces
{{#each interfaces}}
- {{this}}
{{/each}}

## Properties
| Name | Type | Description |
|------|------|-------------|
{{#each properties}}
| {{name}} | {{type}} | {{description}} |
{{/each}}

## Methods
{{#each methods}}
### {{name}}

{{description}}

#### Parameters
| Name | Type | Description | Required | Default |
|------|------|-------------|----------|---------|
{{#each parameters}}
| {{name}} | {{type}} | {{description}} | {{#if isOptional}}No{{else}}Yes{{/if}} | {{defaultValue}} |
{{/each}}

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