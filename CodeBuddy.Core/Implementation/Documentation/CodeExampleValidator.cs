using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Validates code examples to ensure they compile and follow best practices
    /// </summary>
    public class CodeExampleValidator
    {
        private readonly Dictionary<string, ICodeValidator> _validators;

        public CodeExampleValidator()
        {
            _validators = new Dictionary<string, ICodeValidator>
            {
                ["csharp"] = new CSharpCodeValidator(),
                ["javascript"] = new JavaScriptCodeValidator(),
                ["python"] = new PythonCodeValidator()
            };
        }

        /// <summary>
        /// Validates a code example for correctness and best practices
        /// </summary>
        public async Task<ExampleValidationResult> ValidateExample(CodeExample example)
        {
            var result = new ExampleValidationResult { IsValid = true };

            try
            {
                // Validate example metadata
                if (string.IsNullOrEmpty(example.Title))
                {
                    result.IsValid = false;
                    result.Issues.Add("Example title is required");
                }

                if (string.IsNullOrEmpty(example.Description))
                {
                    result.IsValid = false;
                    result.Issues.Add("Example description is required");
                }

                if (string.IsNullOrEmpty(example.Code))
                {
                    result.IsValid = false;
                    result.Issues.Add("Example code is required");
                }

                // Validate code using language-specific validator
                if (_validators.TryGetValue(example.Language.ToLower(), out var validator))
                {
                    var validationResult = await validator.ValidateAsync(example.Code);
                    if (!validationResult.IsValid)
                    {
                        result.IsValid = false;
                        foreach (var error in validationResult.Errors)
                        {
                            result.Issues.Add($"Line {error.Line}: {error.Message}");
                        }
                    }
                }
                else
                {
                    result.Issues.Add($"No validator available for language: {example.Language}");
                }

                // Validate code style and best practices
                var styleIssues = ValidateCodeStyle(example);
                result.Issues.AddRange(styleIssues);
                result.IsValid &= styleIssues.Count == 0;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Issues.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        private List<string> ValidateCodeStyle(CodeExample example)
        {
            var issues = new List<string>();

            // Common style checks
            if (example.Code.Length > 1000)
            {
                issues.Add("Example code is too long (>1000 characters). Consider breaking it into smaller examples.");
            }

            if (example.Code.Split('\n').Length > 50)
            {
                issues.Add("Example code has too many lines (>50). Consider breaking it into smaller examples.");
            }

            // Language-specific style checks
            switch (example.Language.ToLower())
            {
                case "csharp":
                    ValidateCSharpStyle(example.Code, issues);
                    break;
                case "javascript":
                    ValidateJavaScriptStyle(example.Code, issues);
                    break;
                case "python":
                    ValidatePythonStyle(example.Code, issues);
                    break;
            }

            return issues;
        }

        private void ValidateCSharpStyle(string code, List<string> issues)
        {
            // Check for var usage consistency
            if (code.Contains(" var ") && (code.Contains(" string ") || code.Contains(" int ") || code.Contains(" bool ")))
            {
                issues.Add("Inconsistent use of var vs explicit types");
            }

            // Check for proper exception handling
            if (code.Contains("catch (Exception)") || code.Contains("catch(Exception)"))
            {
                issues.Add("Avoid catching general Exception without proper handling");
            }

            // Check for async/await consistency
            if (code.Contains("async") && !code.Contains("await"))
            {
                issues.Add("Async method should contain await operators");
            }
        }

        private void ValidateJavaScriptStyle(string code, List<string> issues)
        {
            // Check for proper Promise handling
            if (code.Contains("new Promise") && !code.Contains(".catch"))
            {
                issues.Add("Promise should include error handling (.catch)");
            }

            // Check for async/await consistency
            if (code.Contains("async") && !code.Contains("await"))
            {
                issues.Add("Async function should use await");
            }

            // Check for proper variable declaration
            if (code.Contains("var "))
            {
                issues.Add("Use const or let instead of var");
            }
        }

        private void ValidatePythonStyle(string code, List<string> issues)
        {
            // Check for proper exception handling
            if (code.Contains("except:") || code.Contains("except :"))
            {
                issues.Add("Avoid bare except clauses");
            }

            // Check for proper resource handling
            if (code.Contains("open(") && !code.Contains("with open"))
            {
                issues.Add("Use 'with' statement for file handling");
            }

            // Check for proper string formatting
            if (code.Contains("%s") || code.Contains("%d"))
            {
                issues.Add("Use f-strings or str.format() instead of % formatting");
            }
        }
    }

    /// <summary>
    /// Represents the result of validating a code example
    /// </summary>
    public class ExampleValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
    }
}