using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security;

public interface ISecurityScanner
{
    Task<SecurityScanResult> ScanAsync(string code, SecurityScanOptions options);
    IEnumerable<string> GetSupportedVulnerabilityTypes();
    bool SupportsLanguage(string language);
}

public class SecurityScanOptions
{
    public int SeverityThreshold { get; set; } = 7;
    public string[] ExcludeVulnerabilityTypes { get; set; } = Array.Empty<string>();
    public bool IncludeRuleDescriptions { get; set; } = true;
    public bool ScanDependencies { get; set; } = true;
    public SecurityScanLevel ScanLevel { get; set; } = SecurityScanLevel.Standard;
}

public enum SecurityScanLevel
{
    Basic,
    Standard,
    Thorough
}

public class SecurityScanResult
{
    public List<SecurityVulnerability> Vulnerabilities { get; set; } = new();
    public bool HasCriticalVulnerabilities => Vulnerabilities.Any(v => v.Severity >= 9);
    public DateTime ScanTime { get; set; }
    public TimeSpan ScanDuration { get; set; }
    public string ScannerVersion { get; set; } = "";
    public Dictionary<string, int> VulnerabilityStatistics { get; set; } = new();
}

public class SecurityVulnerability
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Severity { get; set; }
    public string VulnerabilityType { get; set; } = "";
    public string AffectedCodeLocation { get; set; } = "";
    public string RemediationGuidance { get; set; } = "";
    public string CWE { get; set; } = "";
    public string OWASP { get; set; } = "";
    public Dictionary<string, string> AdditionalInfo { get; set; } = new();
}