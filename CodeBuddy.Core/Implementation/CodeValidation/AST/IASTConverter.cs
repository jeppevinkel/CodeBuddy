using System.Threading.Tasks;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Interface for converting language-specific ASTs to the unified AST format
    /// </summary>
    public interface IASTConverter
    {
        /// <summary>
        /// The programming language this converter handles
        /// </summary>
        string Language { get; }

        /// <summary>
        /// Converts source code to a unified AST representation
        /// </summary>
        /// <param name="sourceCode">The source code to parse</param>
        /// <param name="filePath">Optional path to the source file</param>
        /// <returns>A unified AST representation of the code</returns>
        Task<UnifiedASTNode> ConvertToUnifiedASTAsync(string sourceCode, string filePath = null);

        /// <summary>
        /// Validates whether this converter can handle the given source code
        /// </summary>
        /// <param name="sourceCode">The source code to validate</param>
        /// <returns>True if the converter can handle this code</returns>
        bool CanHandle(string sourceCode);
    }
}