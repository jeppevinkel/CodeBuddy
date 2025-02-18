using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    public class CSharpCodeValidator : BaseCodeValidator
    {
        public CSharpCodeValidator(IValidationCache validationCache = null) 
            : base(new CSharpASTConverter(), validationCache ?? new ValidationCache())
        {
        }

        public override string Language => "C#";

        protected override async Task PerformLanguageSpecificValidation(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            // Add C#-specific validation logic here
            // This might include Roslyn-specific analysis that goes beyond the common patterns

            try
            {
                // Perform Roslyn-specific analysis
                await PerformRoslynAnalysis(result, sourceCode, options);

                // Check for C#-specific patterns
                await CheckCSharpSpecificPatterns(result);
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = $"C# validation error: {ex.Message}",
                    RuleId = "CSHARP_VALIDATION_ERROR"
                });
            }
        }

        private async Task PerformRoslynAnalysis(ValidationResult result, string sourceCode, ValidationOptions options)
        {
            // Here you would integrate with Roslyn analyzers
            // This is just a placeholder for where you'd add the actual implementation
            await Task.CompletedTask;
        }

        private async Task CheckCSharpSpecificPatterns(ValidationResult result)
        {
            var patterns = new[]
            {
                _patternMatcher.CreatePattern("UsingStatement", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "HasDisposeCall", false }
                }),
                _patternMatcher.CreatePattern("LockStatement", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ContainsAwait", true }
                }),
                _patternMatcher.CreatePattern("AsyncMethod", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ReturnsVoid", true }
                })
            };

            foreach (var pattern in patterns)
            {
                var matches = await _patternMatcher.FindMatchesAsync(result.AST, pattern);
                foreach (var match in matches)
                {
                    result.PatternMatches.Add(new ASTPatternMatch
                    {
                        PatternName = $"CSharp_{pattern.NodeType}",
                        MatchedNodes = new System.Collections.Generic.List<Models.AST.UnifiedASTNode> { match },
                        Location = match.Location
                    });
                }
            }
        }
    }
}