using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class JavaScriptCodeValidator : BaseCodeValidator
    {
        public JavaScriptCodeValidator(IValidationCache validationCache = null) 
            : base(new JavaScriptASTConverter(), validationCache ?? new ValidationCache())
        {
        }

        public override string Language => "JavaScript";

        protected override async Task PerformLanguageSpecificValidation(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            try
            {
                // Check for JavaScript-specific patterns
                await CheckJavaScriptSpecificPatterns(result);

                // Perform ESLint-style analysis
                await PerformESLintAnalysis(result, sourceCode, options);
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = $"JavaScript validation error: {ex.Message}",
                    RuleId = "JS_VALIDATION_ERROR"
                });
            }
        }

        private async Task PerformESLintAnalysis(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            // Here you would integrate with ESLint or similar JavaScript linting tools
            // This is just a placeholder for where you'd add the actual implementation
            await Task.CompletedTask;
        }

        private async Task CheckJavaScriptSpecificPatterns(ValidationResult result)
        {
            var patterns = new[]
            {
                _patternMatcher.CreatePattern("Promise", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "HasErrorHandling", false }
                }),
                _patternMatcher.CreatePattern("CallExpression", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Name", "eval" }
                }),
                _patternMatcher.CreatePattern("BinaryExpression", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Operator", "==" }  // Check for non-strict equality
                }),
                _patternMatcher.CreatePattern("FunctionDeclaration", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "UsesThis", true },
                    { "IsArrowFunction", false }
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    result.PatternMatches.Add(new ASTPatternMatch
                    {
                        PatternName = $"JavaScript_{pattern.NodeType}",
                        MatchedNodes = new System.Collections.Generic.List<Models.AST.UnifiedASTNode> { match },
                        Location = match.Location,
                        Context = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "Severity", IssueSeverity.Warning },
                            { "Suggestion", GetSuggestionForPattern(pattern.NodeType) }
                        }
                    });
                }
            }
        }

        private string GetSuggestionForPattern(string patternType)
        {
            switch (patternType)
            {
                case "Promise":
                    return "Add error handling with .catch() or try/catch with async/await";
                case "CallExpression":
                    return "Avoid using eval() as it can lead to security vulnerabilities";
                case "BinaryExpression":
                    return "Use === for strict equality comparison";
                case "FunctionDeclaration":
                    return "Consider using arrow functions to preserve 'this' context";
                default:
                    return string.Empty;
            }
        }
    }
}