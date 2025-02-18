using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for documentation generation system
    /// </summary>
    public interface IDocumentationGenerator
    {
        /// <summary>
        /// Generates complete API documentation
        /// </summary>
        Task<DocumentationResult> GenerateApiDocumentationAsync();

        /// <summary>
        /// Generates plugin system documentation
        /// </summary>
        Task<DocumentationResult> GeneratePluginDocumentationAsync();

        /// <summary>
        /// Generates validation pipeline documentation
        /// </summary>
        Task<DocumentationResult> GenerateValidationDocumentationAsync();

        /// <summary>
        /// Generates cross-component interaction documentation
        /// </summary>
        Task<CrossComponentDocumentation> GenerateCrossComponentDocumentationAsync();

        /// <summary>
        /// Generates implementation patterns and best practices documentation
        /// </summary>
        Task<BestPracticesDocumentation> GenerateBestPracticesDocumentationAsync();

        /// <summary>
        /// Validates documentation completeness and integrity
        /// </summary>
        Task<DocumentationValidationResult> ValidateDocumentationAsync();

        /// <summary>
        /// Generates cross-reference documentation between components
        /// </summary>
        Task<CrossReferenceResult> GenerateCrossReferencesAsync();

        /// <summary>
        /// Analyzes documentation coverage and completeness
        /// </summary>
        Task<DocumentationCoverageResult> AnalyzeDocumentationCoverageAsync();

        /// <summary>
        /// Extracts code examples from tests and XML documentation
        /// </summary>
        Task<List<CodeExample>> ExtractCodeExamplesAsync();

        /// <summary>
        /// Generates resource management patterns documentation
        /// </summary>
        Task<ResourcePatternDocumentation> GenerateResourcePatternsAsync();
    }
}