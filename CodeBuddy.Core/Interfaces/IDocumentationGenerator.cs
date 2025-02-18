using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for the comprehensive documentation generation system
    /// </summary>
    public interface IDocumentationGenerator
    {
        /// <summary>
        /// Generates comprehensive API documentation for the codebase including XML docs,
        /// method signatures, and cross-references
        /// </summary>
        Task<DocumentationResult> GenerateApiDocumentationAsync();

        /// <summary>
        /// Generates documentation for the plugin system including configuration templates
        /// and developer guidelines
        /// </summary>
        Task<DocumentationResult> GeneratePluginDocumentationAsync();

        /// <summary>
        /// Generates documentation for the validation pipeline including error handling
        /// and performance guidelines
        /// </summary>
        Task<DocumentationResult> GenerateValidationDocumentationAsync();

        /// <summary>
        /// Extracts and validates code examples from test cases and documentation
        /// </summary>
        Task<List<CodeExample>> ExtractCodeExamplesAsync();

        /// <summary>
        /// Validates documentation coverage and accuracy across the codebase
        /// </summary>
        Task<DocumentationValidationResult> ValidateDocumentationAsync();

        /// <summary>
        /// Generates cross-reference links between related components
        /// </summary>
        Task<CrossReferenceResult> GenerateCrossReferencesAsync();

        /// <summary>
        /// Analyzes XML documentation coverage and completeness
        /// </summary>
        Task<DocumentationCoverageResult> AnalyzeDocumentationCoverageAsync();

        /// <summary>
        /// Generates resource management and usage pattern documentation
        /// </summary>
        Task<ResourcePatternDocumentation> GenerateResourcePatternsAsync();
    }
}