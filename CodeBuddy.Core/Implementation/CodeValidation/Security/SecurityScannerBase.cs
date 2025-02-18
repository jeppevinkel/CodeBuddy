using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.Security;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.Security
{
    public abstract class SecurityScannerBase
    {
        protected readonly IASTConverter _astConverter;
        protected readonly SecurityScanningConfiguration _config;

        protected SecurityScannerBase(IASTConverter astConverter, SecurityScanningConfiguration config)
        {
            _astConverter = astConverter;
            _config = config;
        }

        public abstract string Language { get; }

        public virtual async Task<SecurityScanResult> ScanAsync(string sourceCode)
        {
            var result = new SecurityScanResult
            {
                Language = Language,
                ScanStartTime = DateTime.UtcNow
            };

            try
            {
                // Convert source to AST for analysis
                var ast = await _astConverter.ConvertToUnifiedASTAsync(sourceCode);

                // Perform security scans
                await ScanForCommonVulnerabilities(ast, result);
                await ScanDependencies(sourceCode, result);
                await ValidateSecureCodingPatterns(ast, result);
                await ValidateSecurityBestPractices(ast, result);

                // Language-specific security checks
                await PerformLanguageSpecificSecurityScans(ast, sourceCode, result);

                // Update final status
                UpdateScanStatus(result);
            }
            catch (Exception ex)
            {
                result.Status = SecurityScanStatus.Error;
                result.Issues.Add(new SecurityViolation
                {
                    Severity = SecuritySeverity.Critical,
                    Category = SecurityViolationCategory.System,
                    Message = $"Security scan failed: {ex.Message}",
                    Description = ex.StackTrace
                });
            }
            finally
            {
                result.ScanEndTime = DateTime.UtcNow;
                result.ScanDuration = result.ScanEndTime - result.ScanStartTime;
            }

            return result;
        }

        protected virtual async Task ScanForCommonVulnerabilities(UnifiedASTNode ast, SecurityScanResult result)
        {
            // Scan for XSS vulnerabilities
            await ScanForXSSVulnerabilities(ast, result);

            // Scan for SQL Injection
            await ScanForSQLInjection(ast, result);

            // Scan for Command Injection
            await ScanForCommandInjection(ast, result);

            // Scan for Path Traversal
            await ScanForPathTraversal(ast, result);
        }

        protected virtual async Task ScanForXSSVulnerabilities(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "HtmlOutput", Attributes = new Dictionary<string, object> { { "Unsanitized", true } } },
                new { Type = "DomManipulation", Attributes = new Dictionary<string, object> { { "DirectInput", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.XSS,
                        Severity = SecuritySeverity.Critical,
                        Message = "Potential XSS vulnerability detected",
                        Description = "Unsanitized user input is being used in HTML output",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ScanForSQLInjection(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "StringConcatenation", Attributes = new Dictionary<string, object> { { "ContainsSQL", true } } },
                new { Type = "DatabaseQuery", Attributes = new Dictionary<string, object> { { "Parameterized", false } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.SQLInjection,
                        Severity = SecuritySeverity.Critical,
                        Message = "Potential SQL Injection vulnerability detected",
                        Description = "Use parameterized queries instead of string concatenation",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ScanForCommandInjection(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "ProcessStart", Attributes = new Dictionary<string, object> { { "UnsafeInput", true } } },
                new { Type = "SystemCommand", Attributes = new Dictionary<string, object> { { "Sanitized", false } } }
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
                        Message = "Potential Command Injection vulnerability detected",
                        Description = "Ensure all command inputs are properly validated and sanitized",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ScanForPathTraversal(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "FileOperation", Attributes = new Dictionary<string, object> { { "PathSanitized", false } } },
                new { Type = "PathCombination", Attributes = new Dictionary<string, object> { { "UserInput", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.PathTraversal,
                        Severity = SecuritySeverity.Critical,
                        Message = "Potential Path Traversal vulnerability detected",
                        Description = "Sanitize and validate all file paths, especially those containing user input",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ScanDependencies(string sourceCode, SecurityScanResult result)
        {
            // Base implementation - override in language-specific scanners
            await Task.CompletedTask;
        }

        protected virtual async Task ValidateSecureCodingPatterns(UnifiedASTNode ast, SecurityScanResult result)
        {
            await ValidateEncryptionUsage(ast, result);
            await ValidateAuthenticationPatterns(ast, result);
            await ValidateAccessControl(ast, result);
        }

        protected virtual async Task ValidateEncryptionUsage(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Encryption", Attributes = new Dictionary<string, object> { { "Algorithm", "MD5" } } },
                new { Type = "Encryption", Attributes = new Dictionary<string, object> { { "Algorithm", "SHA1" } } }
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
                        Message = $"Weak cryptographic algorithm detected: {pattern.Attributes["Algorithm"]}",
                        Description = "Use strong cryptographic algorithms like SHA256 or better",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ValidateAuthenticationPatterns(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "PasswordStorage", Attributes = new Dictionary<string, object> { { "Hashed", false } } },
                new { Type = "Authentication", Attributes = new Dictionary<string, object> { { "PlaintextCredentials", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.Authentication,
                        Severity = SecuritySeverity.High,
                        Message = "Insecure authentication pattern detected",
                        Description = "Ensure proper password hashing and secure credential handling",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ValidateAccessControl(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "AccessControl", Attributes = new Dictionary<string, object> { { "MissingCheck", true } } },
                new { Type = "Authorization", Attributes = new Dictionary<string, object> { { "Direct", true } } }
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
                        Message = "Missing or improper access control",
                        Description = "Implement proper authorization checks before accessing sensitive resources",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ValidateSecurityBestPractices(UnifiedASTNode ast, SecurityScanResult result)
        {
            await ValidateSecureConfiguration(ast, result);
            await ValidateErrorHandling(ast, result);
            await ValidateLogging(ast, result);
        }

        protected virtual async Task ValidateSecureConfiguration(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Configuration", Attributes = new Dictionary<string, object> { { "Sensitive", true }, { "Encrypted", false } } },
                new { Type = "Setting", Attributes = new Dictionary<string, object> { { "Debug", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.Configuration,
                        Severity = SecuritySeverity.Medium,
                        Message = "Insecure configuration setting detected",
                        Description = "Ensure sensitive configuration values are encrypted and debug settings are disabled in production",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ValidateErrorHandling(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "ExceptionHandler", Attributes = new Dictionary<string, object> { { "Detailed", true } } },
                new { Type = "ErrorMessage", Attributes = new Dictionary<string, object> { { "StackTrace", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.ErrorHandling,
                        Severity = SecuritySeverity.Medium,
                        Message = "Insecure error handling detected",
                        Description = "Avoid exposing detailed error information to end users",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected virtual async Task ValidateLogging(UnifiedASTNode ast, SecurityScanResult result)
        {
            var patterns = new[]
            {
                new { Type = "Logging", Attributes = new Dictionary<string, object> { { "SensitiveData", true } } },
                new { Type = "Log", Attributes = new Dictionary<string, object> { { "Unencrypted", true } } }
            };

            foreach (var pattern in patterns)
            {
                var matches = await _astConverter.FindNodesAsync(ast, pattern.Type, pattern.Attributes);
                foreach (var match in matches)
                {
                    result.Issues.Add(new SecurityViolation
                    {
                        Category = SecurityViolationCategory.Logging,
                        Severity = SecuritySeverity.Medium,
                        Message = "Insecure logging practice detected",
                        Description = "Ensure sensitive data is not written to logs in plaintext",
                        Location = match.Location,
                        RelatedNode = match
                    });
                }
            }
        }

        protected abstract Task PerformLanguageSpecificSecurityScans(UnifiedASTNode ast, string sourceCode, SecurityScanResult result);

        protected virtual void UpdateScanStatus(SecurityScanResult result)
        {
            var hasCritical = false;
            var hasHigh = false;
            var hasMedium = false;

            foreach (var issue in result.Issues)
            {
                switch (issue.Severity)
                {
                    case SecuritySeverity.Critical:
                        hasCritical = true;
                        break;
                    case SecuritySeverity.High:
                        hasHigh = true;
                        break;
                    case SecuritySeverity.Medium:
                        hasMedium = true;
                        break;
                }
            }

            result.Status = hasCritical ? SecurityScanStatus.Critical :
                           hasHigh ? SecurityScanStatus.High :
                           hasMedium ? SecurityScanStatus.Medium :
                           result.Issues.Count > 0 ? SecurityScanStatus.Low :
                           SecurityScanStatus.Secure;
        }
    }
}