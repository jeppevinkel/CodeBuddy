using Microsoft.Extensions.Logging;

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

/// <summary>
/// Handles code generation based on templates and configuration
/// </summary>
public interface ICodeGenerator
{
    Task<string> GenerateCodeAsync(Template template, Dictionary<string, object> parameters);
    Task<CodeSnippet> GenerateSnippetAsync(Template template, Dictionary<string, object> parameters);
    Task<bool> ValidateGeneratedCodeAsync(string generatedCode);
}

/// <summary>
/// Manages file system operations with safety checks
/// </summary>
public interface IFileOperations
{
    Task<string> ReadFileAsync(string path);
    Task WriteFileAsync(string path, string content);
    Task<bool> FileExistsAsync(string path);
    Task<IEnumerable<string>> ListFilesAsync(string directory, string searchPattern = "*.*");
}

/// <summary>
/// Handles application configuration and settings
/// </summary>
public interface IConfigurationManager
{
    T GetConfiguration<T>(string section) where T : class, new();
    void SaveConfiguration<T>(string section, T configuration) where T : class;
    bool ValidateConfiguration<T>(T configuration) where T : class;
}

/// <summary>
/// Manages plugin lifecycle and operations
/// </summary>
public interface IPluginManager
{
    Task<IEnumerable<IPlugin>> LoadPluginsAsync(string directory);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    IEnumerable<IPlugin> GetEnabledPlugins();
}

/// <summary>
/// Base interface for plugins
/// </summary>
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(ILogger logger);
    Task ShutdownAsync();
}

namespace CodeBuddy.Core.Models;

/// <summary>
/// Represents a code generation template
/// </summary>
public class Template
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// Represents a generated code snippet
/// </summary>
public class CodeSnippet
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public Template SourceTemplate { get; set; } = null!;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Represents the structure of a project
/// </summary>
public class ProjectStructure
{
    public string ProjectName { get; set; } = string.Empty;
    public string RootDirectory { get; set; } = string.Empty;
    public List<string> SourceDirectories { get; set; } = new();
    public List<string> ResourceDirectories { get; set; } = new();
    public Dictionary<string, string> Configuration { get; set; } = new();
}

/// <summary>
/// Represents application configuration
/// </summary>
public class Configuration
{
    public string TemplatesDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string PluginsDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> DefaultParameters { get; set; } = new();
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public bool EnablePlugins { get; set; } = true;
}