namespace CodeBuddy.Core.Models;

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