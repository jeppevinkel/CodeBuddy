using System.Text;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements code generation functionality using templates
/// </summary>
public class CodeGenerator : ICodeGenerator
{
    private readonly ILogger<CodeGenerator> _logger;

    public CodeGenerator(ILogger<CodeGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateCodeAsync(Template template, Dictionary<string, object> parameters)
    {
        try
        {
            _logger.LogInformation("Generating code using template {Id}", template.Id);
            
            var processedContent = await ProcessTemplateContentAsync(template.Content, parameters);
            
            if (!await ValidateGeneratedCodeAsync(processedContent))
            {
                throw new InvalidOperationException("Generated code validation failed");
            }

            return processedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating code using template {Id}", template.Id);
            throw;
        }
    }

    public async Task<CodeSnippet> GenerateSnippetAsync(Template template, Dictionary<string, object> parameters)
    {
        try
        {
            _logger.LogInformation("Generating code snippet using template {Id}", template.Id);
            
            var content = await GenerateCodeAsync(template, parameters);
            
            var snippet = new CodeSnippet
            {
                Id = Guid.NewGuid().ToString(),
                Content = content,
                Language = template.Language,
                SourceTemplate = template,
                Parameters = parameters,
                GeneratedAt = DateTime.UtcNow
            };

            return snippet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating code snippet using template {Id}", template.Id);
            throw;
        }
    }

    public async Task<bool> ValidateGeneratedCodeAsync(string generatedCode)
    {
        if (string.IsNullOrWhiteSpace(generatedCode))
        {
            _logger.LogWarning("Generated code validation failed: Empty content");
            return false;
        }

        // In a real implementation, this would include:
        // - Syntax validation for the specific language
        // - Security checks
        // - Code style validation
        // For now, we'll just do basic validation

        return await Task.FromResult(true);
    }

    private async Task<string> ProcessTemplateContentAsync(string templateContent, Dictionary<string, object> parameters)
    {
        // This is a simple parameter replacement implementation
        // In a real implementation, this would use a proper template engine
        
        return await Task.Run(() =>
        {
            var result = new StringBuilder(templateContent);
            
            foreach (var (key, value) in parameters)
            {
                result.Replace($"{{{key}}}", value?.ToString() ?? string.Empty);
            }

            return result.ToString();
        });
    }
}