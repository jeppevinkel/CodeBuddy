using System.Text.RegularExpressions;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security;

public class PythonSecurityScanner : BaseSecurityScanner
{
    private static readonly string[] VulnerabilityTypes = new[]
    {
        "SQLInjection",
        "ShellInjection",
        "PickleDeserialization",
        "TimingAttack",
        "UnsafeYAML",
        "PathTraversal",
        "WeakCryptography",
        "UnsafeTemplates"
    };

    public PythonSecurityScanner() : base("Python", "1.0.0") { }

    public override IEnumerable<string> GetSupportedVulnerabilityTypes() => VulnerabilityTypes;

    protected override void InitializeSecurityRules()
    {
        SecurityRules.TryAdd("PY-SEC-001", new SecurityRule
        {
            Id = "PY-SEC-001",
            Title = "SQL Injection",
            Description = "Detects potential SQL injection vulnerabilities through string concatenation",
            DefaultSeverity = 9,
            VulnerabilityType = "SQLInjection",
            CWE = "CWE-89",
            OWASP = "A1:2017",
            DetectionLogic = DetectSQLInjection,
            RemediationTemplate = "Use parameterized queries or ORM"
        });

        SecurityRules.TryAdd("PY-SEC-002", new SecurityRule
        {
            Id = "PY-SEC-002",
            Title = "Shell Injection",
            Description = "Identifies potential shell injection vulnerabilities",
            DefaultSeverity = 9,
            VulnerabilityType = "ShellInjection",
            CWE = "CWE-78",
            OWASP = "A1:2017",
            DetectionLogic = DetectShellInjection,
            RemediationTemplate = "Use subprocess module with shell=False"
        });

        SecurityRules.TryAdd("PY-SEC-003", new SecurityRule
        {
            Id = "PY-SEC-003",
            Title = "Unsafe Pickle Deserialization",
            Description = "Detects usage of unsafe pickle deserialization",
            DefaultSeverity = 8,
            VulnerabilityType = "PickleDeserialization",
            CWE = "CWE-502",
            OWASP = "A8:2017",
            DetectionLogic = DetectUnsafeDeserialization,
            RemediationTemplate = "Use safe serialization formats like JSON"
        });

        SecurityRules.TryAdd("PY-SEC-004", new SecurityRule
        {
            Id = "PY-SEC-004",
            Title = "Timing Attack Vulnerability",
            Description = "Identifies code vulnerable to timing attacks in security-critical comparisons",
            DefaultSeverity = 7,
            VulnerabilityType = "TimingAttack",
            CWE = "CWE-208",
            OWASP = "A6:2017",
            DetectionLogic = DetectTimingAttacks,
            RemediationTemplate = "Use hmac.compare_digest() for secure string comparison"
        });
    }

    protected override async Task<List<SecurityVulnerability>> ScanImplementationAsync(
        string code,
        SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();

        foreach (var rule in SecurityRules.Values)
        {
            if (options.ExcludeVulnerabilityTypes.Contains(rule.VulnerabilityType))
                continue;

            var ruleVulnerabilities = await rule.DetectionLogic(code, options);
            vulnerabilities.AddRange(ruleVulnerabilities);
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectSQLInjection(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"execute\([^)]*\+",
            @"executemany\([^)]*\+",
            @"cursor\.execute\([^)]*%",
            @"\.format\([^)]*\)",
            @"\+\s*[\"']SELECT",
            @"\+\s*[\"']INSERT",
            @"\+\s*[\"']UPDATE",
            @"\+\s*[\"']DELETE"
        };

        foreach (var pattern in dangerousPatterns)
        {
            var matches = Regex.Matches(code, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"PY-SEC-001-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["PY-SEC-001"].Title,
                    Description = SecurityRules["PY-SEC-001"].Description,
                    Severity = SecurityRules["PY-SEC-001"].DefaultSeverity,
                    VulnerabilityType = "SQLInjection",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["PY-SEC-001"].RemediationTemplate,
                    CWE = SecurityRules["PY-SEC-001"].CWE,
                    OWASP = SecurityRules["PY-SEC-001"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectShellInjection(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"os\.system\(",
            @"os\.popen\(",
            @"subprocess\.call\([^,]+,\s*shell\s*=\s*True",
            @"subprocess\.Popen\([^,]+,\s*shell\s*=\s*True",
            @"commands\.getoutput\(",
            @"commands\.getstatusoutput\(",
            @"eval\([^)]+\)"
        };

        foreach (var pattern in dangerousPatterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"PY-SEC-002-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["PY-SEC-002"].Title,
                    Description = SecurityRules["PY-SEC-002"].Description,
                    Severity = SecurityRules["PY-SEC-002"].DefaultSeverity,
                    VulnerabilityType = "ShellInjection",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["PY-SEC-002"].RemediationTemplate,
                    CWE = SecurityRules["PY-SEC-002"].CWE,
                    OWASP = SecurityRules["PY-SEC-002"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectUnsafeDeserialization(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"pickle\.loads?\(",
            @"cPickle\.loads?\(",
            @"yaml\.load\(",
            @"marshal\.loads?\(",
            @"shelve\.open\("
        };

        foreach (var pattern in dangerousPatterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"PY-SEC-003-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["PY-SEC-003"].Title,
                    Description = SecurityRules["PY-SEC-003"].Description,
                    Severity = SecurityRules["PY-SEC-003"].DefaultSeverity,
                    VulnerabilityType = "PickleDeserialization",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["PY-SEC-003"].RemediationTemplate,
                    CWE = SecurityRules["PY-SEC-003"].CWE,
                    OWASP = SecurityRules["PY-SEC-003"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectTimingAttacks(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"==\s*[""'][^""']+[""']",  // String comparison with ==
            @"!=\s*[""'][^""']+[""']",  // String comparison with !=
            @"if\s+password\s*==",      // Password comparison
            @"if\s+token\s*==",         // Token comparison
            @"if\s+key\s*==",           // Key comparison
            @"compare\s*\([^)]+\)"      // Direct compare function usage
        };

        // Look for absence of safe comparison methods
        bool usesSafeComparison = code.Contains("hmac.compare_digest") || 
                                 code.Contains("secrets.compare_digest");

        if (!usesSafeComparison)
        {
            foreach (var pattern in dangerousPatterns)
            {
                var matches = Regex.Matches(code, pattern);
                foreach (Match match in matches)
                {
                    // Check if the comparison is in a security context
                    var surroundingCode = GetSurroundingCode(code, match.Index, 100);
                    if (IsSecurityContext(surroundingCode))
                    {
                        vulnerabilities.Add(new SecurityVulnerability
                        {
                            Id = $"PY-SEC-004-{vulnerabilities.Count + 1}",
                            Title = SecurityRules["PY-SEC-004"].Title,
                            Description = SecurityRules["PY-SEC-004"].Description,
                            Severity = SecurityRules["PY-SEC-004"].DefaultSeverity,
                            VulnerabilityType = "TimingAttack",
                            AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                            RemediationGuidance = SecurityRules["PY-SEC-004"].RemediationTemplate,
                            CWE = SecurityRules["PY-SEC-004"].CWE,
                            OWASP = SecurityRules["PY-SEC-004"].OWASP
                        });
                    }
                }
            }
        }

        return vulnerabilities;
    }

    private string GetSurroundingCode(string code, int position, int radius)
    {
        var start = Math.Max(0, position - radius);
        var end = Math.Min(code.Length, position + radius);
        return code.Substring(start, end - start);
    }

    private bool IsSecurityContext(string code)
    {
        var securityKeywords = new[]
        {
            "password", "token", "secret", "key", "hash", "auth", "crypt",
            "security", "verify", "validation", "authenticate"
        };

        return securityKeywords.Any(keyword => 
            code.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private int GetLineNumber(string code, int position)
    {
        return code.Take(position).Count(c => c == '\n') + 1;
    }
}