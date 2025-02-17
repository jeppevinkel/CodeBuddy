using System.Text.Json;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Implements template management functionality with file-based storage
/// </summary>
public class TemplateManager : ITemplateManager
{
    private readonly IFileOperations _fileOperations;
    private readonly ILogger<TemplateManager> _logger;

    public TemplateManager(IFileOperations fileOperations, ILogger<TemplateManager> logger)
    {
        _fileOperations = fileOperations;
        _logger = logger;
    }

    public async Task<Template> LoadTemplateAsync(string templatePath)
    {
        try
        {
            _logger.LogInformation("Loading template from {Path}", templatePath);
            var content = await _fileOperations.ReadFileAsync(templatePath);
            var template = JsonSerializer.Deserialize<Template>(content) 
                ?? throw new InvalidOperationException("Failed to deserialize template");
            
            if (!ValidateTemplate(template))
            {
                throw new InvalidOperationException("Template validation failed");
            }

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template from {Path}", templatePath);
            throw;
        }
    }

    public async Task<IEnumerable<Template>> LoadTemplatesFromDirectoryAsync(string directoryPath)
    {
        try
        {
            _logger.LogInformation("Loading templates from directory {Path}", directoryPath);
            var files = await _fileOperations.ListFilesAsync(directoryPath, "*.json");
            var templates = new List<Template>();

            foreach (var file in files)
            {
                try
                {
                    var template = await LoadTemplateAsync(file);
                    templates.Add(template);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load template {File}", file);
                }
            }

            return templates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates from directory {Path}", directoryPath);
            throw;
        }
    }

    public async Task SaveTemplateAsync(Template template, string path)
    {
        try
        {
            if (!ValidateTemplate(template))
            {
                throw new InvalidOperationException("Template validation failed");
            }

            _logger.LogInformation("Saving template {Id} to {Path}", template.Id, path);
            template.ModifiedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await _fileOperations.WriteFileAsync(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving template {Id} to {Path}", template.Id, path);
            throw;
        }
    }

    public bool ValidateTemplate(Template template)
    {
        if (string.IsNullOrWhiteSpace(template.Id) ||
            string.IsNullOrWhiteSpace(template.Name) ||
            string.IsNullOrWhiteSpace(template.Content) ||
            string.IsNullOrWhiteSpace(template.Language))
        {
            _logger.LogWarning("Template validation failed: Required fields missing");
            return false;
        }

        if (template.CreatedAt == default)
        {
            template.CreatedAt = DateTime.UtcNow;
        }

        if (template.ModifiedAt == default)
        {
            template.ModifiedAt = DateTime.UtcNow;
        }

        return true;
    }
}