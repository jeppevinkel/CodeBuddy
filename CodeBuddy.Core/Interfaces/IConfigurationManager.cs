namespace CodeBuddy.Core.Interfaces;

/// <summary>
/// Handles application configuration and settings
/// </summary>
public interface IConfigurationManager
{
    T GetConfiguration<T>(string section) where T : class, new();
    void SaveConfiguration<T>(string section, T configuration) where T : class;
    bool ValidateConfiguration<T>(T configuration) where T : class;
}