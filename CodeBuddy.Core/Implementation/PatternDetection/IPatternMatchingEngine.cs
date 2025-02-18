using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Models.Patterns;

namespace CodeBuddy.Core.Implementation.PatternDetection
{
    public interface IPatternMatchingEngine
    {
        Task<List<PatternMatchResult>> DetectPatternsAsync(UnifiedASTNode astRoot, string filePath);
    }
}