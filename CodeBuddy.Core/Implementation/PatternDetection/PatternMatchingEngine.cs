using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.Patterns;
using CodeBuddy.Core.Models.ResourceManagement;

namespace CodeBuddy.Core.Implementation.PatternDetection
{
    public class PatternMatchingEngine : IPatternMatchingEngine
    {
        private readonly IPatternRepository _patternRepository;
        private readonly List<ResourceLeakPattern> _resourceLeakPatterns;

        public PatternMatchingEngine(
            IPatternRepository patternRepository,
            IEnumerable<ResourceLeakPattern> resourceLeakPatterns)
        {
            _patternRepository = patternRepository;
            _resourceLeakPatterns = new List<ResourceLeakPattern>(resourceLeakPatterns);
        }

        public async Task<IEnumerable<PatternMatchResult>> FindPatternsAsync(string code)
        {
            var results = new List<PatternMatchResult>();

            // Get standard patterns
            var standardPatterns = await _patternRepository.GetPatternsAsync();
            foreach (var pattern in standardPatterns)
            {
                if (await MatchPatternAsync(code, pattern))
                {
                    results.Add(new PatternMatchResult
                    {
                        PatternId = pattern.Id,
                        Matched = true,
                        Confidence = 1.0
                    });
                }
            }

            // Check for resource leak patterns
            var resourceLeaks = await FindResourceLeakPatternsAsync(code);
            results.AddRange(resourceLeaks);

            return results;
        }

        private async Task<IEnumerable<PatternMatchResult>> FindResourceLeakPatternsAsync(string code)
        {
            var results = new List<PatternMatchResult>();

            foreach (var pattern in _resourceLeakPatterns)
            {
                var match = await AnalyzeResourceUsagePatternAsync(code, pattern);
                if (match.Confidence > 0)
                {
                    results.Add(match);
                }
            }

            return results;
        }

        private async Task<PatternMatchResult> AnalyzeResourceUsagePatternAsync(
            string code, 
            ResourceLeakPattern pattern)
        {
            var result = new PatternMatchResult
            {
                PatternId = pattern.Id,
                PatternType = "ResourceLeak",
                Matched = false,
                Confidence = 0
            };

            try
            {
                // Check for resource allocation patterns
                var hasAllocation = pattern.AllocationPatterns.Any(p => code.Contains(p));
                
                // Check for proper resource release
                var hasRelease = pattern.ReleasePatterns.Any(p => code.Contains(p));

                // Check for using statement or try-finally patterns
                var hasProperHandling = pattern.ProperHandlingPatterns.Any(p => code.Contains(p));

                // Calculate match confidence
                if (hasAllocation)
                {
                    result.Matched = true;
                    
                    if (!hasRelease)
                    {
                        // Resource allocated but never released
                        result.Confidence = 0.9;
                        result.Description = $"Resource of type {pattern.ResourceType} is allocated but never released";
                    }
                    else if (!hasProperHandling)
                    {
                        // Resource released but not in a safe manner
                        result.Confidence = 0.7;
                        result.Description = $"Resource of type {pattern.ResourceType} might not be properly disposed in all code paths";
                    }
                    else
                    {
                        // Resource appears to be handled correctly
                        result.Confidence = 0;
                    }

                    // Check for potential exception paths
                    if (pattern.ExceptionPatterns.Any(p => code.Contains(p)))
                    {
                        result.Confidence += 0.1;
                        result.Description += " (potential leak in exception paths)";
                    }
                }

                // Add recommended fixes if a potential leak is detected
                if (result.Confidence > 0)
                {
                    result.Fixes = new List<string>
                    {
                        $"Ensure {pattern.ResourceType} is disposed using 'using' statement",
                        $"Implement try-finally block for {pattern.ResourceType} cleanup",
                        $"Consider implementing IDisposable pattern if resource is class-level"
                    };
                }
            }
            catch (Exception ex)
            {
                result.Confidence = 0;
                result.Description = $"Error analyzing resource pattern: {ex.Message}";
            }

            return result;
        }

        private async Task<bool> MatchPatternAsync(string code, CodePattern pattern)
        {
            try
            {
                switch (pattern.Type)
                {
                    case "Regex":
                        return await MatchRegexPatternAsync(code, pattern);
                    case "AST":
                        return await MatchASTPatternAsync(code, pattern);
                    case "Semantic":
                        return await MatchSemanticPatternAsync(code, pattern);
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Task<bool> MatchRegexPatternAsync(string code, CodePattern pattern)
        {
            // Implement regex-based pattern matching
            return Task.FromResult(false);
        }

        private Task<bool> MatchASTPatternAsync(string code, CodePattern pattern)
        {
            // Implement AST-based pattern matching
            return Task.FromResult(false);
        }

        private Task<bool> MatchSemanticPatternAsync(string code, CodePattern pattern)
        {
            // Implement semantic pattern matching
            return Task.FromResult(false);
        }
    }
}