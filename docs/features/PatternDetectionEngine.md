# Pattern Detection Engine Documentation

## Overview
The Pattern Detection Engine is a core component of CodeBuddy that provides powerful pattern matching capabilities across different programming languages. This system allows for definition, detection, and validation of code patterns using a custom Domain-Specific Language (DSL).

## Core Components

### IPatternMatchingEngine
The main interface for pattern matching operations:

```csharp
public interface IPatternMatchingEngine
{
    PatternMatchResult MatchPattern(string sourceCode, CodePattern pattern);
    IEnumerable<PatternMatchResult> FindAllPatterns(string sourceCode, IEnumerable<CodePattern> patterns);
    bool ValidateCodeAgainstPattern(string sourceCode, CodePattern pattern);
}
```

### IPatternRepository
Repository interface for pattern management:

```csharp
public interface IPatternRepository
{
    void AddPattern(CodePattern pattern);
    void RemovePattern(string patternId);
    CodePattern GetPattern(string patternId);
    IEnumerable<CodePattern> GetAllPatterns();
    void UpdatePattern(CodePattern pattern);
}
```

## Pattern DSL Syntax

The Pattern DSL provides a flexible way to define code patterns. Here's the syntax reference:

### Basic Pattern Structure
```
pattern {
    name: "Pattern Name"
    id: "unique-pattern-id"
    language: ["csharp", "javascript"] // supported languages
    severity: "warning"
    description: "Pattern description"
    match {
        // pattern matching rules
    }
}
```

### Matching Rules

#### 1. Direct Text Matching
```
match {
    text: "exact code to match"
    ignoreWhitespace: true/false
    caseSensitive: true/false
}
```

#### 2. Regular Expression Matching
```
match {
    regex: "pattern-regex"
    flags: "im" // regex flags
}
```

#### 3. AST Pattern Matching
```
match {
    ast {
        type: "MethodDeclaration"
        modifiers: ["public", "static"]
        returnType: "void"
        parameters: [
            { type: "string", name: "*" }
        ]
    }
}
```

## Integration Examples

### 1. Basic Pattern Matching

```csharp
var engine = serviceProvider.GetService<IPatternMatchingEngine>();
var pattern = new CodePattern 
{
    Id = "empty-catch-block",
    Name = "Empty Catch Block Detection",
    Description = "Detects empty catch blocks",
    Language = "csharp",
    MatchPattern = @"
        match {
            ast {
                type: 'CatchClause'
                body: { statements: [] }
            }
        }
    "
};

var result = engine.MatchPattern(sourceCode, pattern);
if (result.IsMatch)
{
    Console.WriteLine($"Found empty catch block at line {result.Location.Line}");
}
```

### 2. Custom Pattern Repository Usage

```csharp
var repository = serviceProvider.GetService<IPatternRepository>();

// Add new pattern
repository.AddPattern(new CodePattern 
{
    Id = "singleton-pattern",
    Name = "Singleton Implementation",
    Description = "Detects singleton pattern implementation",
    Language = "csharp",
    MatchPattern = @"
        match {
            ast {
                type: 'ClassDeclaration'
                modifiers: ['public']
                members: [
                    {
                        type: 'FieldDeclaration'
                        modifiers: ['private', 'static']
                    },
                    {
                        type: 'PropertyDeclaration'
                        modifiers: ['public', 'static']
                    }
                ]
            }
        }
    "
});

// Retrieve and use pattern
var pattern = repository.GetPattern("singleton-pattern");
var results = engine.MatchPattern(sourceCode, pattern);
```

## Extending Pattern Matching Capabilities

### 1. Creating Custom Pattern Matchers

```csharp
public class CustomPatternMatcher : IPatternMatcher
{
    public PatternMatchResult Match(string sourceCode, CodePattern pattern)
    {
        // Custom matching logic
        return new PatternMatchResult 
        {
            IsMatch = true,
            Location = new CodeLocation { Line = 1, Column = 1 },
            MatchedText = "..."
        };
    }
}
```

### 2. Registering Custom Matchers

```csharp
services.AddSingleton<IPatternMatcher, CustomPatternMatcher>();
```

## Performance Considerations

1. **Pattern Complexity**
   - Keep pattern definitions focused and specific
   - Use AST matching for structural patterns
   - Use text/regex matching for simple patterns

2. **Caching**
   - Pattern compilation results are cached
   - Repository implements pattern caching
   - Match results can be cached for repeated analysis

3. **Resource Usage**
   - AST parsing is memory-intensive
   - Consider batch processing for large codebases
   - Use pattern repository to avoid redundant pattern loading

## Best Practices

1. **Pattern Design**
   - Write specific, focused patterns
   - Include clear descriptions
   - Provide example matches and non-matches
   - Version patterns appropriately

2. **Error Handling**
   - Always validate pattern syntax before use
   - Handle pattern matching timeouts
   - Implement fallback strategies

3. **Integration**
   - Use dependency injection
   - Implement proper error handling
   - Cache pattern matching results
   - Log pattern matching metrics

## Common Use Cases

1. **Code Quality Checks**
   - Empty catch blocks
   - Magic number usage
   - Long method detection
   - Complexity thresholds

2. **Security Patterns**
   - SQL injection vulnerabilities
   - Insecure crypto usage
   - Path traversal risks

3. **Architecture Compliance**
   - Layer violation detection
   - Dependency rules
   - Naming conventions

## Troubleshooting

### Common Issues and Solutions

1. **Pattern Not Matching**
   - Verify pattern syntax
   - Check language settings
   - Validate AST structure
   - Enable debug logging

2. **Performance Issues**
   - Review pattern complexity
   - Check caching configuration
   - Monitor memory usage
   - Consider batch processing

3. **Integration Problems**
   - Verify service registration
   - Check dependency versions
   - Review configuration
   - Enable detailed logging

## Version Compatibility

| Pattern Engine Version | CodeBuddy Version | Key Features |
|-----------------------|-------------------|--------------|
| 1.0.x                | 1.0.0 - 1.1.0     | Basic matching |
| 1.1.x                | 1.2.0 - 1.3.0     | AST support |
| 2.0.x                | 2.0.0+            | Multi-language |

## Pattern Repository Management

### Organization
- Group patterns by purpose
- Use consistent naming
- Include metadata
- Version control patterns

### Maintenance
- Regular pattern review
- Performance monitoring
- Usage analytics
- Pattern deprecation

### Sharing
- Pattern export/import
- Team collaboration
- Pattern libraries
- Version control

## Additional Resources

1. Source Code
   - [PatternMatchingEngine.cs](/CodeBuddy/CodeBuddy.Core/Implementation/PatternDetection/PatternMatchingEngine.cs)
   - [PatternRepository.cs](/CodeBuddy/CodeBuddy.Core/Implementation/PatternDetection/PatternRepository.cs)

2. Test Examples
   - [PatternMatchingEngineTests.cs](/CodeBuddy/CodeBuddy.Tests/PatternDetection/PatternMatchingEngineTests.cs)

3. Related Documentation
   - [Cross-Language Type System](/docs/features/CrossLanguageTypeSystem.md)
   - [Validation Pipeline](/docs/features/ValidationPipeline.md)