using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for generating comprehensive documentation
    /// </summary>
    public interface IDocumentationGenerator
    {
        /// <summary>
        /// Generates complete API documentation for the codebase
        /// </summary>
        Task<DocumentationResult> GenerateApiDocumentationAsync();
        
        /// <summary>
        /// Generates documentation for the plugin system
        /// </summary>
        Task<DocumentationResult> GeneratePluginDocumentationAsync();
        
        /// <summary>
        /// Generates documentation for the validation pipeline
        /// </summary>
        Task<DocumentationResult> GenerateValidationDocumentationAsync();
        
        /// <summary>
        /// Generates comprehensive documentation for features and architecture.
        /// </summary>
        Task<DocumentationResult> GenerateFeatureDocumentationAsync();
        
        /// <summary>
        /// Generates documentation for configuration options.
        /// </summary>
        Task<DocumentationResult> GenerateConfigurationDocumentationAsync();
        
        /// <summary>
        /// Generates TypeScript type definitions from C# code.
        /// </summary>
        Task<DocumentationResult> GenerateTypeScriptDefinitionsAsync();
        
        /// <summary>
        /// Validates the generated documentation
        /// </summary>
        Task<DocumentationValidationResult> ValidateDocumentationAsync();
        
        /// <summary>
        /// Generates cross-references between documentation elements
        /// </summary>
        Task<CrossReferenceResult> GenerateCrossReferencesAsync();
        
        /// <summary>
        /// Analyzes documentation coverage
        /// </summary>
        Task<DocumentationCoverageResult> AnalyzeDocumentationCoverageAsync();
        
        /// <summary>
        /// Generates documentation for resource management patterns
        /// </summary>
        Task<ResourcePatternDocumentation> GenerateResourcePatternsAsync();
    }
}