# {{name}} Plugin

**Version:** {{version}}

{{description}}

## Overview
This document describes how to use and configure the {{name}} plugin.

## Features
{{#each features}}
- {{this}}
{{/each}}

## Installation
```bash
{{installCommand}}
```

## Configuration
```json
{
{{#each configOptions}}
  "{{name}}": {{defaultValue}}, // {{description}}
{{/each}}
}
```

## Usage Examples
{{#each examples}}
### {{title}}
{{description}}

```{{language}}
{{code}}
```
{{/each}}

## API Reference
{{#each api}}
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
No parameters required.
{{/if}}

#### Returns
{{returnType}} - {{returnDescription}}
{{/each}}

## Troubleshooting
{{#each troubleshooting}}
### {{problem}}
{{solution}}
{{/each}}

## Known Issues
{{#each issues}}
- {{this}}
{{/each}}