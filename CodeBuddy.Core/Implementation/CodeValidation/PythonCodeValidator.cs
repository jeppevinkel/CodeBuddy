using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class PythonCodeValidator : BaseCodeValidator
    {
        public PythonCodeValidator(IValidationCache validationCache = null) 
            : base(new PythonASTConverter(), validationCache ?? new ValidationCache())
        {
        }

        public override string Language => "Python";

        protected override async Task PerformLanguageSpecificValidation(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            try
            {
                // Check for Python-specific patterns
                await CheckPythonSpecificPatterns(result);

                // Perform Pylint-style analysis
                await PerformPylintAnalysis(result, sourceCode, options);
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = $"Python validation error: {ex.Message}",
                    RuleId = "PYTHON_VALIDATION_ERROR"
                });
            }
        }

        private async Task PerformPylintAnalysis(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            // Here you would integrate with Pylint or similar Python linting tools
            // This is just a placeholder for where you'd add the actual implementation
            await Task.CompletedTask;
        }

        private async Task CheckPythonSpecificPatterns(ValidationResult result)
        {
            var patterns = new[]
            {
                _patternMatcher.CreatePattern("WithStatement", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "HasExceptionHandler", false }
                }),
                _patternMatcher.CreatePattern("ComprehensionExpression", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Nested", true }
                }),
                _patternMatcher.CreatePattern("ExceptHandler", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ExceptionType", "Exception" }  // Too broad exception handling
                }),
                _patternMatcher.CreatePattern("Import", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "IsWildcard", true }  // from module import *
                }),
                _patternMatcher.CreatePattern("ClassDefinition", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "HasMutableDefault", true }  // Mutable default arguments
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    var issue = new ValidationIssue
                    {
                        Severity = GetSeverityForPattern(pattern.NodeType),
                        Message = GetMessageForPattern(pattern.NodeType),
                        Location = match.Location,
                        RelatedNodes = new System.Collections.Generic.List<Models.AST.UnifiedASTNode> { match },
                        RuleId = $"PY_{pattern.NodeType.ToUpper()}"
                    };

                    result.Issues.Add(issue);

                    result.PatternMatches.Add(new ASTPatternMatch
                    {
                        PatternName = $"Python_{pattern.NodeType}",
                        MatchedNodes = new System.Collections.Generic.List<Models.AST.UnifiedASTNode> { match },
                        Location = match.Location,
                        Context = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "Severity", issue.Severity },
                            { "Suggestion", GetSuggestionForPattern(pattern.NodeType) }
                        }
                    });
                }
            }
        }

        private IssueSeverity GetSeverityForPattern(string patternType)
        {
            switch (patternType)
            {
                case "WithStatement":
                case "ExceptHandler":
                    return IssueSeverity.Warning;
                case "Import":
                    return IssueSeverity.Error;
                case "ClassDefinition":
                    return IssueSeverity.Warning;
                case "ComprehensionExpression":
                    return IssueSeverity.Info;
                default:
                    return IssueSeverity.Info;
            }
        }

        private string GetMessageForPattern(string patternType)
        {
            switch (patternType)
            {
                case "WithStatement":
                    return "Resource management without exception handling";
                case "ComprehensionExpression":
                    return "Complex nested comprehension may reduce readability";
                case "ExceptHandler":
                    return "Too broad exception handling";
                case "Import":
                    return "Wildcard imports are discouraged";
                case "ClassDefinition":
                    return "Mutable default argument used";
                default:
                    return $"Python-specific issue: {patternType}";
            }
        }

        private string GetSuggestionForPattern(string patternType)
        {
            switch (patternType)
            {
                case "WithStatement":
                    return "Add try/except block around the with statement";
                case "ComprehensionExpression":
                    return "Consider breaking down complex comprehensions into separate steps";
                case "ExceptHandler":
                    return "Catch specific exceptions instead of using bare except";
                case "Import":
                    return "Import specific names instead of using wildcard imports";
                case "ClassDefinition":
                    return "Use None as default and initialize mutable values in __init__";
                default:
                    return string.Empty;
            }
        }
    }
}