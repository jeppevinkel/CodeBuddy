using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Security;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security
{
    public class JavaScriptSecurityScanner : SecurityScannerBase
    {
        public JavaScriptSecurityScanner(IASTConverter astConverter, SecurityScanningConfiguration config)
            : base(astConverter, config)
        {
        }

        public override string Language => "JavaScript";

        protected override async Task PerformLanguageSpecificSecurityScans(UnifiedASTNode ast, string sourceCode, SecurityScanResult result)
        {
            await ScanForDOMXSS(ast, result);
            await ScanForEvalUsage(ast, result);
            await ScanForInsecureRandomness(ast, result);
            await ScanForClientSideValidation(ast, result);
            await ScanForInsecurePostMessageHandling(ast, result);
            await ScanForStorageAPIs(ast, result);
        }

        private async Task ScanForDOMXSS(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Assignment", Attributes = new Dictionary<string, object> { { "Property", "innerHTML" } } },
                new { Type = "Assignment", Attributes = new Dictionary<string, object> { { "Property", "outerHTML" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "document.write" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.XSS,
                        Severity = SecuritySeverity.High,
                        Message = "DOM-based XSS vulnerability detected",
                        Description = "Direct manipulation of HTML content can lead to Cross-Site Scripting attacks",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-79",
                        Recommendation = "Use safe DOM APIs like textContent or sanitize HTML content"
                    });
                }
            }
        }

        private async Task ScanForEvalUsage(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "eval" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "Function" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "setTimeout" }, { "StringArgument", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.CommandInjection,
                        Severity = SecuritySeverity.Critical,
                        Message = "Use of eval() or similar dynamic code execution detected",
                        Description = "Dynamic code execution can lead to code injection vulnerabilities",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-95",
                        Recommendation = "Avoid using eval() and similar functions. Use safer alternatives."
                    });
                }
            }
        }

        private async Task ScanForInsecureRandomness(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "Math.random" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.WeakCrypto,
                        Severity = SecuritySeverity.Medium,
                        Message = "Use of weak random number generator",
                        Description = "Math.random() is not cryptographically secure",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-338",
                        Recommendation = "Use crypto.getRandomValues() for cryptographic operations"
                    });
                }
            }
        }

        private async Task ScanForClientSideValidation(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "FunctionDeclaration", Attributes = new Dictionary<string, object> { { "Name", "validate" }, { "ClientSideOnly", true } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "ValidationContext", "client" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.AccessControl,
                        Severity = SecuritySeverity.Medium,
                        Message = "Client-side only validation detected",
                        Description = "Relying solely on client-side validation is insecure",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-602",
                        Recommendation = "Always implement server-side validation"
                    });
                }
            }
        }

        private async Task ScanForInsecurePostMessageHandling(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "EventListener", Attributes = new Dictionary<string, object> { { "Event", "message" }, { "OriginCheck", false } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "postMessage" }, { "TargetOrigin", "*" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.AccessControl,
                        Severity = SecuritySeverity.High,
                        Message = "Insecure postMessage handling",
                        Description = "Missing origin validation in postMessage handling",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-346",
                        Recommendation = "Always verify message origin and avoid using '*' as target origin"
                    });
                }
            }
        }

        private async Task ScanForStorageAPIs(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Object", "localStorage" }, { "SensitiveData", true } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Object", "sessionStorage" }, { "SensitiveData", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.DataLeakage,
                        Severity = SecuritySeverity.Medium,
                        Message = "Sensitive data stored in browser storage",
                        Description = "Storing sensitive data in localStorage/sessionStorage can expose it to XSS attacks",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-922",
                        Recommendation = "Avoid storing sensitive data in browser storage"
                    });
                }
            }
        }

        protected override async Task ScanDependencies(string sourceCode, SecurityScanResult result)
        {
            // Scan package.json and yarn.lock/package-lock.json if available
            // Implementation would check npm packages against known vulnerability databases
            await base.ScanDependencies(sourceCode, result);
        }
    }
}