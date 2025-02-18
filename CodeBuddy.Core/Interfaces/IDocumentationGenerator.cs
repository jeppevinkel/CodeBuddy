using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for the documentation generation system
    /// </summary>
    public interface IDocumentationGenerator
    {
        /// <summary>
        /// Generates API documentation for the codebase
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
    }
}