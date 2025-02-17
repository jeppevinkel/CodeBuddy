using System.Text;
using CodeBuddy.Core.Implementation.CodeValidation;
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
    private readonly CodeValidatorFactory _validatorFactory;

    public CodeGenerator(ILogger<CodeGenerator> logger)
    {
        _logger = logger;
        _validatorFactory = new CodeValidatorFactory(logger);
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

    public async Task<bool> ValidateGeneratedCodeAsync(string generatedCode, string language = "csharp")
    {
        if (string.IsNullOrWhiteSpace(generatedCode))
        {
            _logger.LogWarning("Generated code validation failed: Empty content");
            return false;
        }

        try
        {
            if (!_validatorFactory.SupportsLanguage(language))
            {
                _logger.LogWarning("Language {Language} is not supported for validation", language);
                return true; // Return true for unsupported languages to avoid blocking generation
            }

            var validator = _validatorFactory.GetValidator(language);
            var options = new ValidationOptions
            {
                ValidateSyntax = true,
                ValidateSecurity = true,
                ValidateStyle = true,
                ValidateBestPractices = true,
                ValidateErrorHandling = true
            };

            var result = await validator.ValidateAsync(generatedCode, language, options);

            if (!result.IsValid)
            {
                foreach (var issue in result.Issues)
                {
                    _logger.LogWarning("Validation issue: [{Severity}] {Code} - {Message} at {Location}",
                        issue.Severity, issue.Code, issue.Message, issue.Location);
                }
            }

            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during code validation");
            throw;
        }
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