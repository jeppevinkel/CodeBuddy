# {{name}} Plugin

Version: {{version}}

{{description}}

## Dependencies
{{#each dependencies}}
- {{this}}
{{/each}}

## Configuration
```json
{{{configuration}}}
```

## Interfaces
{{#each interfaces}}
### {{name}}

{{description}}

#### Methods
{{#each methods}}
##### {{name}}

{{description}}
{{/each}}
{{/each}}

## Examples
{{#each examples}}
### {{title}}

{{description}}

```{{language}}
{{code}}
```
{{/each}}