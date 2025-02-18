using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Security;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security
{
    public class PythonSecurityScanner : SecurityScannerBase
    {
        public PythonSecurityScanner(IASTConverter astConverter, SecurityScanningConfiguration config)
            : base(astConverter, config)
        {
        }

        public override string Language => "Python";

        protected override async Task PerformLanguageSpecificSecurityScans(UnifiedASTNode ast, string sourceCode, SecurityScanResult result)
        {
            await ScanForDangerousBuiltins(ast, result);
            await ScanForShellInjection(ast, result);
            await ScanForPickleUsage(ast, result);
            await ScanForTemplateInjection(ast, result);
            await ScanForRequestValidation(ast, result);
            await ScanForDjangoSecurityIssues(ast, result);
        }

        private async Task ScanForDangerousBuiltins(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "eval" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Name", "exec" } } },
                new { Type = "Import", Attributes = new Dictionary<string, object> { { "Module", "__builtin__" } } }
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
                        Message = "Use of dangerous built-in function detected",
                        Description = "Using eval(), exec() or __builtin__ can lead to code injection vulnerabilities",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-95",
                        Recommendation = "Avoid using eval() and similar functions. Use safer alternatives."
                    });
                }
            }
        }

        private async Task ScanForShellInjection(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Import", Attributes = new Dictionary<string, object> { { "Module", "os" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "os" }, { "Name", "system" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "subprocess" }, { "Name", "call" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.CommandInjection,
                        Severity = SecuritySeverity.High,
                        Message = "Potential shell injection vulnerability",
                        Description = "Using os.system() or subprocess with shell=True can lead to command injection",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-78",
                        Recommendation = "Use subprocess.run() with shell=False and properly escaped arguments"
                    });
                }
            }
        }

        private async Task ScanForPickleUsage(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Import", Attributes = new Dictionary<string, object> { { "Module", "pickle" } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "pickle" }, { "Name", "loads" } } }
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
                        Message = "Unsafe deserialization using pickle",
                        Description = "Using pickle.loads() with untrusted data can lead to code execution",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-502",
                        Recommendation = "Use safe serialization formats like JSON"
                    });
                }
            }
        }

        private async Task ScanForTemplateInjection(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "jinja2" }, { "Name", "Template" } } },
                new { Type = "StringFormatting", Attributes = new Dictionary<string, object> { { "DynamicInput", true } } }
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
                        Message = "Potential template injection vulnerability",
                        Description = "Unsafe template rendering can lead to server-side template injection",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-94",
                        Recommendation = "Use template auto-escaping and avoid dynamic template compilation"
                    });
                }
            }
        }

        private async Task ScanForRequestValidation(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "requests" }, { "Name", "get" }, { "VerifySSL", false } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "urllib" }, { "Name", "urlopen" }, { "Validation", false } } }
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
                        Message = "Insecure HTTP request handling",
                        Description = "SSL certificate verification is disabled or missing input validation",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-295",
                        Recommendation = "Always verify SSL certificates and validate request parameters"
                    });
                }
            }
        }

        private async Task ScanForDjangoSecurityIssues(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Assignment", Attributes = new Dictionary<string, object> { { "Variable", "DEBUG" }, { "Value", true } } },
                new { Type = "MethodInvocation", Attributes = new Dictionary<string, object> { { "Module", "django.db.models" }, { "Name", "raw" } } },
                new { Type = "Assignment", Attributes = new Dictionary<string, object> { { "Setting", "ALLOWED_HOSTS" }, { "Value", "*" } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.Configuration,
                        Severity = SecuritySeverity.High,
                        Message = "Django security misconfiguration",
                        Description = "Insecure Django settings or usage patterns detected",
                        Location = match.Location,
                        RelatedNode = match,
                        CWE = "CWE-16",
                        Recommendation = "Follow Django security best practices and properly configure security settings"
                    });
                }
            }
        }

        protected override async Task ScanDependencies(string sourceCode, SecurityScanResult result)
        {
            // Scan requirements.txt or Pipfile if available
            // Implementation would check PyPI packages against known vulnerability databases
            await base.ScanDependencies(sourceCode, result);
        }
    }
}