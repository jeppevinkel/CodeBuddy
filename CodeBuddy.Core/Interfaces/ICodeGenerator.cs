using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Handles code generation based on templates and configuration
/// </summary>
public interface ICodeGenerator
{
    Task<string> GenerateCodeAsync(Template template, Dictionary<string, object> parameters);
    Task<CodeSnippet> GenerateSnippetAsync(Template template, Dictionary<string, object> parameters);
    Task<bool> ValidateGeneratedCodeAsync(string generatedCode);
}