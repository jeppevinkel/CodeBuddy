using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Implementation.CodeValidation.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public abstract class BaseCodeValidator : ICodeValidator
    {
        protected readonly IASTConverter _astConverter;
        protected readonly ASTPatternMatcher _patternMatcher;
        protected readonly IValidationCache _validationCache;

        protected BaseCodeValidator(IASTConverter astConverter, IValidationCache validationCache)
        {
            _astConverter = astConverter;
            _patternMatcher = new ASTPatternMatcher();
            _validationCache = validationCache;
        }

        public abstract string Language { get; }

        public virtual async Task<ValidationResult> ValidateAsync(string sourceCode, ValidationOptions options = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new ValidationResult
            {
                Language = Language
            };

            try
            {
                // Check cache first
                var cacheKey = GetCacheKey(sourceCode);
                var cachedResult = await _validationCache.GetAsync(cacheKey);
                if (cachedResult != null)
                {
                    return cachedResult;
                }

                // Parse the code into unified AST
                result.AST = await _astConverter.ConvertToUnifiedASTAsync(sourceCode);

                // Perform language-specific validation
                await PerformLanguageSpecificValidation(result, sourceCode, options);

                // Perform cross-language pattern matching
                await PerformPatternMatching(result, options);

                // Update status based on issues
                UpdateValidationStatus(result);

                // Cache the result
                await _validationCache.SetAsync(cacheKey, result);
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Error;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = $"Validation failed: {ex.Message}",
                    RuleId = "INTERNAL_ERROR"
                });
            }
            finally
            {
                sw.Stop();
                result.ValidationTime = sw.Elapsed;
            }

            return result;
        }

        protected abstract Task PerformLanguageSpecificValidation(ValidationResult result, string sourceCode, ValidationOptions options);

        protected virtual async Task PerformPatternMatching(ValidationResult result, ValidationOptions options)
        {
            if (result.AST == null)
                return;

            // Apply common patterns
            await ApplySecurityPatterns(result);
            await ApplyCodeQualityPatterns(result);
            await ApplyPerformancePatterns(result);
            
            // Apply custom patterns if specified in options
            if (options?.CustomPatterns != null)
            {
                foreach (var pattern in options.CustomPatterns)
                {
                    var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                    foreach (var match in matches)
                    {
                        result.PatternMatches.Add(new ASTPatternMatch
                        {
                            PatternName = pattern.NodeType,
                            MatchedNodes = new List<UnifiedASTNode> { match },
                            Location = match.Location
                        });
                    }
                }
            }
        }

        protected virtual async Task ApplySecurityPatterns(ValidationResult result)
        {
            // Example security patterns
            var patterns = new List<ASTPatternMatcher.ASTPattern>
            {
                // SQL Injection pattern
                _patternMatcher.CreatePattern("StringConcatenation", new Dictionary<string, object>
                {
                    { "ContainsVariable", true },
                    { "ContainsSQL", true }
                }),
                
                // XSS pattern
                _patternMatcher.CreatePattern("HtmlOutput", new Dictionary<string, object>
                {
                    { "Unsanitized", true }
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Message = $"Potential security vulnerability: {pattern.NodeType}",
                        Location = match.Location,
                        RelatedNodes = new List<UnifiedASTNode> { match }
                    });
                }
            }
        }

        protected virtual async Task ApplyCodeQualityPatterns(ValidationResult result)
        {
            var patterns = new List<ASTPatternMatcher.ASTPattern>
            {
                // Long method pattern
                _patternMatcher.CreatePattern("Function", new Dictionary<string, object>
                {
                    { "LineCount", 50 } // threshold
                }),
                
                // Complex condition pattern
                _patternMatcher.CreatePattern("Condition", new Dictionary<string, object>
                {
                    { "Complexity", 5 } // threshold
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Message = $"Code quality issue: {pattern.NodeType}",
                        Location = match.Location,
                        RelatedNodes = new List<UnifiedASTNode> { match }
                    });
                }
            }
        }

        protected virtual async Task ApplyPerformancePatterns(ValidationResult result)
        {
            var patterns = new List<ASTPatternMatcher.ASTPattern>
            {
                // Inefficient loop pattern
                _patternMatcher.CreatePattern("Loop", new Dictionary<string, object>
                {
                    { "ContainsNestedLoop", true }
                }),
                
                // Memory leak pattern
                _patternMatcher.CreatePattern("ResourceUsage", new Dictionary<string, object>
                {
                    { "Disposed", false }
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Message = $"Performance issue: {pattern.NodeType}",
                        Location = match.Location,
                        RelatedNodes = new List<UnifiedASTNode> { match }
                    });
                }
            }
        }

        protected virtual void UpdateValidationStatus(ValidationResult result)
        {
            var hasCritical = false;
            var hasError = false;
            var hasWarning = false;

            foreach (var issue in result.Issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Critical:
                        hasCritical = true;
                        break;
                    case IssueSeverity.Error:
                        hasError = true;
                        break;
                    case IssueSeverity.Warning:
                        hasWarning = true;
                        break;
                }
            }

            if (hasCritical || hasError)
                result.Status = ValidationStatus.Error;
            else if (hasWarning)
                result.Status = ValidationStatus.Warning;
            else
                result.Status = ValidationStatus.Success;
        }

        protected virtual string GetCacheKey(string sourceCode)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(sourceCode);
                var hash = sha.ComputeHash(bytes);
                return $"{Language}_{Convert.ToBase64String(hash)}";
            }
        }
    }
}