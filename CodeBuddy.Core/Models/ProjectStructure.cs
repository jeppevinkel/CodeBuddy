namespace CodeBuddy.Core.Models;

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