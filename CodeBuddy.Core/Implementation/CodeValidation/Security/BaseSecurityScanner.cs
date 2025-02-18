using System.Collections.Concurrent;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security;

public abstract class BaseSecurityScanner : ISecurityScanner
{
    protected readonly ConcurrentDictionary<string, SecurityRule> SecurityRules = new();
    protected readonly string Language;
    protected readonly string Version;

    protected BaseSecurityScanner(string language, string version)
    {
        Language = language;
        Version = version;
        InitializeSecurityRules();
    }

    public async Task<SecurityScanResult> ScanAsync(string code, SecurityScanOptions options)
    {
        var result = new SecurityScanResult
        {
            ScanTime = DateTime.UtcNow,
            ScannerVersion = Version
        };

        var startTime = DateTime.UtcNow;

        try
        {
            // Run language-specific pre-scan analysis
            await PreScanAnalysis(code);

            // Perform actual security scanning
            var vulnerabilities = await ScanImplementationAsync(code, options);
            
            // Filter out excluded vulnerability types and those below severity threshold
            result.Vulnerabilities = vulnerabilities
                .Where(v => !options.ExcludeVulnerabilityTypes.Contains(v.VulnerabilityType))
                .Where(v => v.Severity >= options.SeverityThreshold)
                .ToList();

            // Calculate statistics
            result.VulnerabilityStatistics = result.Vulnerabilities
                .GroupBy(v => v.VulnerabilityType)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        finally
        {
            result.ScanDuration = DateTime.UtcNow - startTime;
        }

        return result;
    }

    public abstract IEnumerable<string> GetSupportedVulnerabilityTypes();

    public bool SupportsLanguage(string language) => 
        Language.Equals(language, StringComparison.OrdinalIgnoreCase);

    protected abstract Task<List<SecurityVulnerability>> ScanImplementationAsync(
        string code, 
        SecurityScanOptions options);

    protected abstract void InitializeSecurityRules();

    protected virtual Task PreScanAnalysis(string code) => Task.CompletedTask;

    protected class SecurityRule
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int DefaultSeverity { get; set; }
        public string VulnerabilityType { get; set; } = "";
        public string CWE { get; set; } = "";
        public string OWASP { get; set; } = "";
        public Func<string, SecurityScanOptions, Task<List<SecurityVulnerability>>> DetectionLogic { get; set; } = null!;
        public string RemediationTemplate { get; set; } = "";
    }
}