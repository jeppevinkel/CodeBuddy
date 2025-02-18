namespace CodeBuddy.Core.Models;

public class ValidatorRegistryConfig
{
    public string[] AutoDiscoveryPaths { get; set; } = Array.Empty<string>();
    public bool EnableHotReload { get; set; } = true;
    public int FileChangeDelayMs { get; set; } = 100;
    public bool EnableHealthChecks { get; set; } = true;
    public int HealthCheckIntervalMs { get; set; } = 30000;
}