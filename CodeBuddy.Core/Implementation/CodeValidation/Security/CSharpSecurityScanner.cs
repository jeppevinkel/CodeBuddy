using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security;

public class CSharpSecurityScanner : BaseSecurityScanner
{
    private static readonly string[] VulnerabilityTypes = new[]
    {
        "UnsafeDeserialization",
        "LDAPInjection",
        "XMLExternalEntity",
        "WeakCryptography",
        "SQLInjection",
        "PathTraversal",
        "InsecureRandomness",
        "HardcodedCredentials"
    };

    public CSharpSecurityScanner() : base("C#", "1.0.0") { }

    public override IEnumerable<string> GetSupportedVulnerabilityTypes() => VulnerabilityTypes;

    protected override void InitializeSecurityRules()
    {
        SecurityRules.TryAdd("CS-SEC-001", new SecurityRule
        {
            Id = "CS-SEC-001",
            Title = "Unsafe Deserialization",
            Description = "Identifies use of unsafe deserialization methods that could lead to remote code execution",
            DefaultSeverity = 9,
            VulnerabilityType = "UnsafeDeserialization",
            CWE = "CWE-502",
            OWASP = "A8:2017",
            DetectionLogic = DetectUnsafeDeserialization,
            RemediationTemplate = "Use secure serialization methods like System.Text.Json or protobuf"
        });

        SecurityRules.TryAdd("CS-SEC-002", new SecurityRule
        {
            Id = "CS-SEC-002",
            Title = "LDAP Injection",
            Description = "Detects potential LDAP injection vulnerabilities",
            DefaultSeverity = 8,
            VulnerabilityType = "LDAPInjection",
            CWE = "CWE-90",
            OWASP = "A1:2017",
            DetectionLogic = DetectLDAPInjection,
            RemediationTemplate = "Use LDAP filters and proper input sanitization"
        });

        SecurityRules.TryAdd("CS-SEC-003", new SecurityRule
        {
            Id = "CS-SEC-003",
            Title = "XML External Entity Attack",
            Description = "Identifies XML parsing configurations vulnerable to XXE attacks",
            DefaultSeverity = 8,
            VulnerabilityType = "XMLExternalEntity",
            CWE = "CWE-611",
            OWASP = "A4:2017",
            DetectionLogic = DetectXXEVulnerability,
            RemediationTemplate = "Disable external entity processing in XML parsers"
        });

        SecurityRules.TryAdd("CS-SEC-004", new SecurityRule
        {
            Id = "CS-SEC-004",
            Title = "Weak Cryptography",
            Description = "Detects use of weak or outdated cryptographic algorithms",
            DefaultSeverity = 7,
            VulnerabilityType = "WeakCryptography",
            CWE = "CWE-326",
            OWASP = "A3:2017",
            DetectionLogic = DetectWeakCryptography,
            RemediationTemplate = "Use strong encryption algorithms and key sizes"
        });
    }

    protected override async Task<List<SecurityVulnerability>> ScanImplementationAsync(
        string code,
        SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();

        // Parse the code
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = await tree.GetRootAsync();

        // Run all security rules
        foreach (var rule in SecurityRules.Values)
        {
            if (options.ExcludeVulnerabilityTypes.Contains(rule.VulnerabilityType))
                continue;

            var ruleVulnerabilities = await rule.DetectionLogic(code, options);
            vulnerabilities.AddRange(ruleVulnerabilities);
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectUnsafeDeserialization(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var unsafePatterns = new[]
        {
            @"BinaryFormatter\.Deserialize",
            @"XmlSerializer\.Deserialize",
            @"JavaScriptSerializer\.Deserialize",
            @"JsonConvert\.DeserializeObject(?!\s*<[^>]*>)"
        };

        foreach (var pattern in unsafePatterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"CS-SEC-001-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["CS-SEC-001"].Title,
                    Description = SecurityRules["CS-SEC-001"].Description,
                    Severity = SecurityRules["CS-SEC-001"].DefaultSeverity,
                    VulnerabilityType = "UnsafeDeserialization",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["CS-SEC-001"].RemediationTemplate,
                    CWE = SecurityRules["CS-SEC-001"].CWE,
                    OWASP = SecurityRules["CS-SEC-001"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectLDAPInjection(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var patterns = new[]
        {
            @"DirectorySearcher\.Filter\s*=\s*[^""]*\+",
            @"DirectoryEntry\.Path\s*=\s*[^""]*\+"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"CS-SEC-002-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["CS-SEC-002"].Title,
                    Description = SecurityRules["CS-SEC-002"].Description,
                    Severity = SecurityRules["CS-SEC-002"].DefaultSeverity,
                    VulnerabilityType = "LDAPInjection",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["CS-SEC-002"].RemediationTemplate,
                    CWE = SecurityRules["CS-SEC-002"].CWE,
                    OWASP = SecurityRules["CS-SEC-002"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectXXEVulnerability(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var patterns = new[]
        {
            @"XmlTextReader(?!\s*\([^)]*\)\s*{\s*XmlResolver\s*=\s*null)",
            @"XmlDocument(?!\s*\([^)]*\)\s*{\s*XmlResolver\s*=\s*null)",
            @"\.XmlResolver\s*=\s*new\s+XmlUrlResolver"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"CS-SEC-003-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["CS-SEC-003"].Title,
                    Description = SecurityRules["CS-SEC-003"].Description,
                    Severity = SecurityRules["CS-SEC-003"].DefaultSeverity,
                    VulnerabilityType = "XMLExternalEntity",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["CS-SEC-003"].RemediationTemplate,
                    CWE = SecurityRules["CS-SEC-003"].CWE,
                    OWASP = SecurityRules["CS-SEC-003"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectWeakCryptography(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var patterns = new[]
        {
            @"MD5\.",
            @"SHA1\.",
            @"DES\.",
            @"new\s+RijndaelManaged",
            @"TripleDES",
            @"new\s+AesCryptoServiceProvider\(\)\s*{\s*KeySize\s*=\s*128",
            @"System\.Random"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"CS-SEC-004-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["CS-SEC-004"].Title,
                    Description = SecurityRules["CS-SEC-004"].Description,
                    Severity = SecurityRules["CS-SEC-004"].DefaultSeverity,
                    VulnerabilityType = "WeakCryptography",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["CS-SEC-004"].RemediationTemplate,
                    CWE = SecurityRules["CS-SEC-004"].CWE,
                    OWASP = SecurityRules["CS-SEC-004"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private int GetLineNumber(string code, int position)
    {
        return code.Take(position).Count(c => c == '\n') + 1;
    }
}