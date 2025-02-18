# Cross-Language Type System Documentation

## Overview

CodeBuddy's cross-language type system provides a sophisticated mechanism for handling type validation and conversion between different programming languages. The system ensures type safety while allowing seamless integration between multiple programming languages in a single codebase.

## Core Components

### 1. UnifiedTypeSystem

The `UnifiedTypeSystem` serves as the central component for type mapping and validation across languages. It provides:

- Unified type representation across languages
- Type compatibility checking
- Type conversion rules and validation
- Data loss detection and prevention
- Language-specific type mapping

### 2. CrossLanguageTypeRule

Defines rules for type conversion and validation between languages, including:

- Source and target type specifications
- Implicit conversion rules
- Custom validation logic
- Conversion warnings and suggestions

### 3. ASTSemanticValidator

Implements semantic validation for Abstract Syntax Tree nodes with cross-language type checking capabilities:

- Type inference
- Cross-language type validation
- Semantic analysis
- Relationship validation

## Type Compatibility Matrix

| Source Type | Target Type | Compatibility | Notes |
|-------------|-------------|---------------|-------|
| C# int      | JS number   | Safe         | Direct conversion possible |
| JS number   | C# int      | Unsafe       | Possible precision loss |
| Python int  | C# int      | Unsafe       | Possible overflow (Python has unlimited precision) |
| C# string   | Python str  | Safe         | Direct conversion possible |
| C# Array    | Python list | Safe         | Element types need separate validation |
| C# Dictionary | JS Object | Safe         | Key/value types need separate validation |

## Usage Guide

### 1. Defining Type Conversion Rules

```csharp
var typeRule = new CrossLanguageTypeRule
{
    SourceType = "int",
    TargetType = "number",
    IsImplicitlyConvertible = true,
    ValidationLogic = (sourceNode, targetNode, context) =>
    {
        // Custom validation logic
        return true;
    }
};
```

### 2. Handling Edge Cases

- Null Values:
  ```csharp
  if (sourceTypeInfo.IsNullable && !targetTypeInfo.IsNullable)
  {
      // Handle null case
      // Add null check or provide default value
  }
  ```

- Data Loss Prevention:
  ```csharp
  var (canConvert, warning) = _typeSystem.CanConvertWithoutLoss(
      sourceLanguage, sourceType,
      targetLanguage, targetType);
  if (!canConvert)
  {
      // Handle potential data loss
  }
  ```

### 3. Type Validation

```csharp
// Check basic type compatibility
if (_typeSystem.AreTypesCompatible(
    sourceLanguage, sourceType,
    targetLanguage, targetType))
{
    // Types are compatible
}

// Get conversion suggestions
var suggestions = _typeSystem.GetConversionSuggestions(
    sourceLanguage, sourceType,
    targetLanguage, targetType);
```

## Best Practices

1. **Type Checking**:
   - Always verify type compatibility before conversion
   - Handle nullability explicitly
   - Check for potential data loss

2. **Error Handling**:
   - Implement proper error handling for type mismatches
   - Provide clear error messages with suggestions
   - Log type conversion warnings

3. **Performance Considerations**:
   - Cache type compatibility results when possible
   - Minimize type conversion operations
   - Use implicit conversions when safe

4. **Extensibility**:
   - Follow the established pattern when adding new language support
   - Implement comprehensive type mapping for new languages
   - Add appropriate conversion rules and validations

## Adding Support for New Languages

1. **Register Language Types**:
   ```csharp
   private void RegisterNewLanguageTypes()
   {
       var newLangTypes = new Dictionary<string, UnifiedType>
       {
           ["customType"] = new UnifiedType("Category", bitSize, isNullable)
       };
       _typeMap["NewLanguage"] = newLangTypes;
   }
   ```

2. **Define Conversion Rules**:
   ```csharp
   private void RegisterNewLanguageConversions()
   {
       _conversionMap[("NewLanguage:type", "ExistingLanguage:type")] =
           new TypeConversionInfo
           {
               IsSafe = true,
               Warning = null,
               Suggestions = new[] { "Conversion guideline" }
           };
   }
   ```

3. **Implement Validation Logic**:
   - Add language-specific validation rules
   - Define type compatibility checks
   - Implement custom conversion logic if needed

## Troubleshooting Guide

### Common Issues and Solutions

1. **Incompatible Type Conversion**
   - **Symptom**: Type conversion error between languages
   - **Solution**: Check type compatibility matrix and use appropriate conversion methods

2. **Data Loss in Conversion**
   - **Symptom**: Loss of precision or data in type conversion
   - **Solution**: Use `CanConvertWithoutLoss` to check before conversion and handle appropriately

3. **Null Reference Issues**
   - **Symptom**: Null reference exceptions in cross-language operations
   - **Solution**: Verify nullability compatibility and add null checks

4. **Generic Type Conversion Failures**
   - **Symptom**: Error converting generic types between languages
   - **Solution**: Ensure generic parameter counts match and types are compatible

## Performance Optimization

1. **Caching Strategies**:
   - Cache type compatibility results
   - Store frequently used type conversion rules
   - Maintain conversion suggestion cache

2. **Validation Optimization**:
   - Use early validation to fail fast
   - Implement batch validation for multiple conversions
   - Optimize type inference algorithms

3. **Resource Management**:
   - Pool commonly used type objects
   - Implement lazy loading for type information
   - Clean up temporary conversion artifacts

## Extension Points

The type system provides several extension points for customization:

1. **Custom Type Rules**:
   - Add new type conversion rules
   - Implement custom validation logic
   - Define language-specific type handling

2. **Validation Hooks**:
   - Pre-validation hooks
   - Post-validation hooks
   - Custom error handling

3. **Type Mapping**:
   - Custom type mapping rules
   - Type aliasing support
   - Complex type conversion logic

## Common Use Cases

1. **API Integration**:
   ```csharp
   // Converting API response types
   var jsResponse = GetJavaScriptResponse();
   var csharpModel = _typeSystem.Convert(jsResponse, "C#");
   ```

2. **Cross-Language Data Transfer**:
   ```csharp
   // Transferring data between language boundaries
   var pythonList = GetPythonData();
   var csharpArray = ValidateAndConvert(pythonList, "Array");
   ```

3. **Type Validation Pipeline**:
   ```csharp
   // Validating types in a pipeline
   var validator = new ASTSemanticValidator(_registry, _typeSystem);
   var result = validator.ValidateNode(node);
   HandleValidationResult(result);
   ```

## Error Codes Reference

| Code    | Description                     | Severity |
|---------|--------------------------------|----------|
| TYPE001 | Undeclared variable            | Error    |
| TYPE002 | Undeclared method              | Error    |
| TYPE003 | Parameter count mismatch       | Error    |
| TYPE004 | Incompatible types            | Error    |
| TYPE005 | Type conversion warning        | Warning  |

This documentation provides a comprehensive guide to understanding and using the cross-language type system effectively. For specific implementation details, refer to the source code and inline documentation in the codebase.