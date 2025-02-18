using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Security;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security
{
    public class CSharpSecurityScanner : SecurityScannerBase
    {
        public CSharpSecurityScanner(IASTConverter astConverter, SecurityScanningConfiguration config)
            : base(astConverter, config)
        {
        }

        public override string Language => "C#";

        protected override async Task PerformLanguageSpecificSecurityScans(UnifiedASTNode ast, string sourceCode, SecurityScanResult result)
        {
            await ScanForUnsafeCode(ast, result);
            await ScanForReflection(ast, result);
            await ScanForDeserializationVulnerabilities(ast, result);
            await ScanForCryptoAPIMisuse(ast, result);
            await ScanForIdentityAPIMisuse(ast, result);
            await ScanForDataProtectionIssues(ast, result);
        }

        private async Task ScanForUnsafeCode(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "UnsafeBlock", Attributes = new Dictionary<string, object>() },
                new { Type = "PointerOperation", Attributes = new Dictionary<string, object>() }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.System,
                        Severity = SecuritySeverity.High,
                        Message = "Unsafe code block detected",
                        Description = "Using unsafe code can lead to memory corruption and security vulnerabilities",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-119"
                    });
                }
            }
        }

        private async Task ScanForReflection(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "Invoke" } } },
                new { Type = "TypeLoading", Attributes = new Dictionary<string, object> { { "Dynamic", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.System,
                        Severity = SecuritySeverity.Medium,
                        Message = "Potentially dangerous reflection usage detected",
                        Description = "Dynamic method invocation can lead to security vulnerabilities if input is not properly validated",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-470"
                    });
                }
            }
        }

        private async Task ScanForDeserializationVulnerabilities(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "BinaryFormatter.Deserialize" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "XmlSerializer.Deserialize" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "JsonConvert.DeserializeObject" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.System,
                        Severity = SecuritySeverity.High,
                        Message = "Potentially unsafe deserialization detected",
                        Description = "Deserializing untrusted data can lead to remote code execution vulnerabilities",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-502",
                        Recommendation = "Use safe deserialization methods like System.Text.Json or implement type validation"
                    });
                }
            }
        }

        private async Task ScanForCryptoAPIMisuse(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "ObjectCreation", Attributes = new Dictionary<string, object> { { "Type", "MD5" } } },
                new { Type = "ObjectCreation", Attributes = new Dictionary<string, object> { { "Type", "SHA1" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "DESCryptoServiceProvider" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.WeakCrypto,
                        Severity = SecuritySeverity.High,
                        Message = $"Use of weak cryptographic algorithm: {pattern.Attributes["Type"] ?? pattern.Attributes["Name"]}",
                        Description = "Using outdated or weak cryptographic algorithms can compromise security",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-327",
                        Recommendation = "Use strong algorithms like AES for encryption and SHA256 or better for hashing"
                    });
                }
            }
        }

        private async Task ScanForIdentityAPIMisuse(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "SignInManager.PasswordSignInAsync" }, { "PasswordHasherOptions", false } } },
                new { Type = "PropertyAccess", Attributes = new Dictionary<string, object> { { "Name", "SecurityStampValidationInterval" }, { "Value", "high" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.Authentication,
                        Severity = SecuritySeverity.Medium,
                        Message = "Potential Identity API misconfiguration",
                        Description = "Insecure configuration of ASP.NET Core Identity can weaken security",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-287"
                    });
                }
            }
        }

        private async Task ScanForDataProtectionIssues(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "IDataProtector.Protect" }, { "KeyRotation", false } } },
                new { Type = "ObjectCreation", Attributes = new Dictionary<string, object> { { "Type", "DataProtectionOptions" }, { "Unprotected", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.DataLeakage,
                        Severity = SecuritySeverity.High,
                        Message = "Data protection configuration issue detected",
                        Description = "Improper configuration of ASP.NET Core Data Protection can expose sensitive data",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-311"
                    });
                }
            }
        }

        protected override async Task ScanDependencies(string sourceCode, SecurityScanResult result)
        {
            // Scan .csproj or packages.config file if available
            // Implementation would check NuGet packages against known vulnerability databases
            await base.ScanDependencies(sourceCode, result);
        }
    }
}