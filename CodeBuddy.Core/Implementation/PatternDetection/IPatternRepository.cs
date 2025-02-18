using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Patterns;

namespace CodeBuddy.Core.Implementation.PatternDetection
{
    public interface IPatternRepository
    {
        Task<IEnumerable<CodePattern>> GetPatternsAsync();
        Task<CodePattern> GetPatternByIdAsync(string id);
        Task AddPatternAsync(CodePattern pattern);
        Task UpdatePatternAsync(CodePattern pattern);
        Task DeletePatternAsync(string id);
    }
}