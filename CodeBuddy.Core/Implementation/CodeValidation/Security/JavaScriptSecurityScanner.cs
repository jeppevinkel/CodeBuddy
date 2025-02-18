using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security;

public class JavaScriptSecurityScanner : BaseSecurityScanner
{
    private static readonly string[] VulnerabilityTypes = new[]
    {
        "CrossSiteScripting",
        "PrototypePollution",
        "CrossSiteRequestForgery",
        "InsecureDependencies",
        "DOMBasedXSS",
        "UnsafeEval",
        "InsecureRandomness",
        "NodesecurityVulnerabilities"
    };

    public JavaScriptSecurityScanner() : base("JavaScript", "1.0.0") { }

    public override IEnumerable<string> GetSupportedVulnerabilityTypes() => VulnerabilityTypes;

    protected override void InitializeSecurityRules()
    {
        SecurityRules.TryAdd("JS-SEC-001", new SecurityRule
        {
            Id = "JS-SEC-001",
            Title = "Cross-Site Scripting (XSS)",
            Description = "Detects potential XSS vulnerabilities through dangerous DOM manipulation",
            DefaultSeverity = 8,
            VulnerabilityType = "CrossSiteScripting",
            CWE = "CWE-79",
            OWASP = "A7:2017",
            DetectionLogic = DetectXSSVulnerabilities,
            RemediationTemplate = "Use safe DOM APIs and proper input sanitization"
        });

        SecurityRules.TryAdd("JS-SEC-002", new SecurityRule
        {
            Id = "JS-SEC-002",
            Title = "Prototype Pollution",
            Description = "Identifies code patterns vulnerable to prototype pollution attacks",
            DefaultSeverity = 7,
            VulnerabilityType = "PrototypePollution",
            CWE = "CWE-915",
            OWASP = "A6:2017",
            DetectionLogic = DetectPrototypePollution,
            RemediationTemplate = "Use Object.create(null) and avoid recursive merge operations"
        });

        SecurityRules.TryAdd("JS-SEC-003", new SecurityRule
        {
            Id = "JS-SEC-003",
            Title = "Cross-Site Request Forgery",
            Description = "Detects missing CSRF protections in form submissions and AJAX requests",
            DefaultSeverity = 8,
            VulnerabilityType = "CrossSiteRequestForgery",
            CWE = "CWE-352",
            OWASP = "A8:2017",
            DetectionLogic = DetectCSRFVulnerabilities,
            RemediationTemplate = "Implement CSRF tokens and validate request origins"
        });

        SecurityRules.TryAdd("JS-SEC-004", new SecurityRule
        {
            Id = "JS-SEC-004",
            Title = "Insecure Dependencies",
            Description = "Analyzes package.json for known vulnerable dependencies",
            DefaultSeverity = 7,
            VulnerabilityType = "InsecureDependencies",
            CWE = "CWE-937",
            OWASP = "A9:2017",
            DetectionLogic = DetectInsecureDependencies,
            RemediationTemplate = "Update dependencies to secure versions and implement version pinning"
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

    private async Task<List<SecurityVulnerability>> DetectXSSVulnerabilities(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"\.innerHTML\s*=",
            @"document\.write\(",
            @"\.outerHTML\s*=",
            @"\.insertAdjacentHTML\(",
            @"eval\(",
            @"\$\([^)]+\)\.html\("
        };

        foreach (var pattern in dangerousPatterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"JS-SEC-001-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["JS-SEC-001"].Title,
                    Description = SecurityRules["JS-SEC-001"].Description,
                    Severity = SecurityRules["JS-SEC-001"].DefaultSeverity,
                    VulnerabilityType = "CrossSiteScripting",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["JS-SEC-001"].RemediationTemplate,
                    CWE = SecurityRules["JS-SEC-001"].CWE,
                    OWASP = SecurityRules["JS-SEC-001"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectPrototypePollution(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"Object\.assign\([^,]+,\s*[^)]+\)",
            @"\.extend\([^,]+,\s*[^)]+\)",
            @"(?<!Object\.create\()null",
            @"for\s*\(\s*(?:let|var)\s+\w+\s+in\s+",
            @"__proto__",
            @"prototype"
        };

        foreach (var pattern in dangerousPatterns)
        {
            var matches = Regex.Matches(code, pattern);
            foreach (Match match in matches)
            {
                vulnerabilities.Add(new SecurityVulnerability
                {
                    Id = $"JS-SEC-002-{vulnerabilities.Count + 1}",
                    Title = SecurityRules["JS-SEC-002"].Title,
                    Description = SecurityRules["JS-SEC-002"].Description,
                    Severity = SecurityRules["JS-SEC-002"].DefaultSeverity,
                    VulnerabilityType = "PrototypePollution",
                    AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                    RemediationGuidance = SecurityRules["JS-SEC-002"].RemediationTemplate,
                    CWE = SecurityRules["JS-SEC-002"].CWE,
                    OWASP = SecurityRules["JS-SEC-002"].OWASP
                });
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectCSRFVulnerabilities(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();
        var dangerousPatterns = new[]
        {
            @"fetch\([^,)]+\)",
            @"XMLHttpRequest",
            @"\$\.(?:get|post|ajax)\(",
            @"axios\.(?:get|post|put|delete)\("
        };

        // Also check for absence of CSRF protection headers
        bool hasCSRFProtection = code.Contains("X-CSRF-TOKEN") || 
                                code.Contains("csrf-token") ||
                                code.Contains("_csrf");

        if (!hasCSRFProtection)
        {
            foreach (var pattern in dangerousPatterns)
            {
                var matches = Regex.Matches(code, pattern);
                foreach (Match match in matches)
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Id = $"JS-SEC-003-{vulnerabilities.Count + 1}",
                        Title = SecurityRules["JS-SEC-003"].Title,
                        Description = SecurityRules["JS-SEC-003"].Description,
                        Severity = SecurityRules["JS-SEC-003"].DefaultSeverity,
                        VulnerabilityType = "CrossSiteRequestForgery",
                        AffectedCodeLocation = $"Line {GetLineNumber(code, match.Index)}",
                        RemediationGuidance = SecurityRules["JS-SEC-003"].RemediationTemplate,
                        CWE = SecurityRules["JS-SEC-003"].CWE,
                        OWASP = SecurityRules["JS-SEC-003"].OWASP
                    });
                }
            }
        }

        return vulnerabilities;
    }

    private async Task<List<SecurityVulnerability>> DetectInsecureDependencies(string code, SecurityScanOptions options)
    {
        var vulnerabilities = new List<SecurityVulnerability>();

        try
        {
            if (code.Contains("package.json"))
            {
                var packageJson = JObject.Parse(code);
                var dependencies = packageJson["dependencies"] as JObject;
                var devDependencies = packageJson["devDependencies"] as JObject;

                // This is a simplified check - in a real implementation, 
                // you would check against a vulnerability database
                var knownVulnerableVersions = new Dictionary<string, string[]>
                {
                    { "jquery", new[] { "<3.0.0" } },
                    { "lodash", new[] { "<4.17.21" } },
                    { "express", new[] { "<4.17.1" } },
                    { "moment", new[] { "<2.29.2" } }
                };

                void CheckDependencies(JObject deps)
                {
                    if (deps == null) return;

                    foreach (var dep in deps)
                    {
                        if (knownVulnerableVersions.TryGetValue(dep.Key, out var versions))
                        {
                            foreach (var version in versions)
                            {
                                if (IsVersionVulnerable(dep.Value.ToString(), version))
                                {
                                    vulnerabilities.Add(new SecurityVulnerability
                                    {
                                        Id = $"JS-SEC-004-{vulnerabilities.Count + 1}",
                                        Title = $"Vulnerable Dependency: {dep.Key}",
                                        Description = $"Using potentially vulnerable version of {dep.Key}: {dep.Value}",
                                        Severity = SecurityRules["JS-SEC-004"].DefaultSeverity,
                                        VulnerabilityType = "InsecureDependencies",
                                        AffectedCodeLocation = $"package.json: {dep.Key}@{dep.Value}",
                                        RemediationGuidance = $"Update {dep.Key} to a version newer than {version}",
                                        CWE = SecurityRules["JS-SEC-004"].CWE,
                                        OWASP = SecurityRules["JS-SEC-004"].OWASP
                                    });
                                }
                            }
                        }
                    }
                }

                CheckDependencies(dependencies);
                CheckDependencies(devDependencies);
            }
        }
        catch (Exception)
        {
            // Silently skip if package.json parsing fails
        }

        return vulnerabilities;
    }

    private bool IsVersionVulnerable(string currentVersion, string vulnerableVersion)
    {
        // Simplified version comparison - in real implementation use proper semver comparison
        if (vulnerableVersion.StartsWith("<"))
        {
            var targetVersion = vulnerableVersion.TrimStart('<');
            return string.Compare(currentVersion, targetVersion, StringComparison.Ordinal) < 0;
        }
        return false;
    }

    private int GetLineNumber(string code, int position)
    {
        return code.Take(position).Count(c => c == '\n') + 1;
    }
}