using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Manages template operations including loading, parsing, and validation
/// </summary>
public interface ITemplateManager
{
    Task<Template> LoadTemplateAsync(string templatePath);
    Task<IEnumerable<Template>> LoadTemplatesFromDirectoryAsync(string directoryPath);
    Task SaveTemplateAsync(Template template, string path);
    bool ValidateTemplate(Template template);
}