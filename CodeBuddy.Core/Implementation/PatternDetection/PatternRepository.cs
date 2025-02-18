using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Models.Patterns;

namespace CodeBuddy.Core.Implementation.PatternDetection
{
    public class PatternRepository : IPatternRepository
    {
        private readonly Dictionary<string, CodePattern> _patterns;
        private readonly string _patternDefinitionsPath;

        public PatternRepository(string patternDefinitionsPath)
        {
            _patternDefinitionsPath = patternDefinitionsPath;
            _patterns = new Dictionary<string, CodePattern>();
            LoadDefaultPatterns();
        }

        public async Task<IEnumerable<CodePattern>> GetPatternsAsync()
        {
            return _patterns.Values;
        }

        public async Task<CodePattern> GetPatternByIdAsync(string id)
        {
            return _patterns.TryGetValue(id, out var pattern) ? pattern : null;
        }

        public async Task AddPatternAsync(CodePattern pattern)
        {
            if (string.IsNullOrEmpty(pattern.Id))
            {
                pattern.Id = Guid.NewGuid().ToString();
            }

            _patterns[pattern.Id] = pattern;
            await SavePatternsAsync();
        }

        public async Task UpdatePatternAsync(CodePattern pattern)
        {
            if (_patterns.ContainsKey(pattern.Id))
            {
                _patterns[pattern.Id] = pattern;
                await SavePatternsAsync();
            }
        }

        public async Task DeletePatternAsync(string id)
        {
            if (_patterns.ContainsKey(id))
            {
                _patterns.Remove(id);
                await SavePatternsAsync();
            }
        }

        private void LoadDefaultPatterns()
        {
            // Add built-in patterns for common issues
            AddSecurityPatterns();
            AddPerformancePatterns();
            AddBestPracticePatterns();
        }

        private void AddSecurityPatterns()
        {
            var sqlInjectionPattern = new CodePattern
            {
                Id = "SEC001",
                Name = "SQL Injection Vulnerability",
                Description = "Detects potential SQL injection vulnerabilities",
                Type = PatternType.Security,
                Severity = PatternSeverity.Critical,
                PatternExpression = "string_concat(sql_query, user_input)",
                IsFuzzyMatching = true,
                MinConfidenceThreshold = 0.8
            };

            _patterns[sqlInjectionPattern.Id] = sqlInjectionPattern;
        }

        private void AddPerformancePatterns()
        {
            var nestedLoopPattern = new CodePattern
            {
                Id = "PERF001",
                Name = "Nested Loop Performance Issue",
                Description = "Detects nested loops that might cause performance problems",
                Type = PatternType.Performance,
                Severity = PatternSeverity.Warning,
                PatternExpression = "loop(loop(*))",
                IsFuzzyMatching = false,
                MinConfidenceThreshold = 1.0
            };

            _patterns[nestedLoopPattern.Id] = nestedLoopPattern;
        }

        private void AddBestPracticePatterns()
        {
            var resourceManagementPattern = new CodePattern
            {
                Id = "BP001",
                Name = "Resource Management Best Practice",
                Description = "Ensures proper resource disposal in using statements",
                Type = PatternType.BestPractice,
                Severity = PatternSeverity.Warning,
                PatternExpression = "new_disposable(!using(*))",
                IsFuzzyMatching = true,
                MinConfidenceThreshold = 0.9
            };

            _patterns[resourceManagementPattern.Id] = resourceManagementPattern;
        }

        private async Task SavePatternsAsync()
        {
            // Implement pattern persistence
        }
    }
}