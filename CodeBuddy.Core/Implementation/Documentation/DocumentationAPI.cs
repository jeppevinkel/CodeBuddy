using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Provides a simple API for documentation operations
    /// </summary>
    public class DocumentationAPI
    {
        private readonly DocumentationGenerator _generator;
        private readonly DocumentationValidator _validator;
        private readonly DocumentationVersionManager _versionManager;

        public DocumentationAPI(
            DocumentationGenerator generator,
            DocumentationValidator validator,
            DocumentationVersionManager versionManager)
        {
            _generator = generator;
            _validator = validator;
            _versionManager = versionManager;
        }

        /// <summary>
        /// Generates complete documentation
        /// </summary>
        public async Task<DocumentationResult> GenerateDocumentationAsync(GenerationOptions options = null)
        {
            try
            {
                var result = new DocumentationResult();

                // Generate API documentation
                var apiDocs = await _generator.GenerateApiDocumentationAsync();
                result.ApiDocumentation = apiDocs;

                // Generate plugin documentation
                if (options?.IncludePlugins != false)
                {
                    var pluginDocs = await _generator.GeneratePluginDocumentationAsync();
                    result.PluginDocumentation = pluginDocs;
                }

                // Generate validation documentation
                if (options?.IncludeValidation != false)
                {
                    var validationDocs = await _generator.GenerateValidationDocumentationAsync();
                    result.ValidationDocumentation = validationDocs;
                }

                // Create new version
                if (options?.CreateVersion != false)
                {
                    var version = await _versionManager.CreateVersionAsync(
                        options?.Version ?? DateTime.UtcNow.ToString("yyyyMMdd.HHmmss"),
                        options?.VersionDescription ?? "Documentation update");
                    result.Version = version;
                }

                // Validate documentation if requested
                if (options?.ValidateDocumentation == true)
                {
                    result.ValidationResult = await _validator.ValidateAsync(new DocumentationSet
                    {
                        Types = apiDocs.Types,
                        Examples = apiDocs.Examples,
                        MarkdownFiles = result.GetAllMarkdownFiles(),
                        Diagrams = result.GetAllDiagrams()
                    });
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                return new DocumentationResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Validates existing documentation
        /// </summary>
        public async Task<ValidationResult> ValidateDocumentationAsync()
        {
            try
            {
                var docs = await LoadCurrentDocumentationAsync();
                return await _validator.ValidateAsync(docs);
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets documentation version history
        /// </summary>
        public async Task<VersionHistoryResult> GetVersionHistoryAsync()
        {
            try
            {
                var versions = await _versionManager.GetVersionsAsync();
                return new VersionHistoryResult
                {
                    Success = true,
                    Versions = versions
                };
            }
            catch (Exception ex)
            {
                return new VersionHistoryResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets documentation for a specific version
        /// </summary>
        public async Task<DocumentationArchive> GetVersionAsync(string version)
        {
            return await _versionManager.GetVersionAsync(version);
        }

        /// <summary>
        /// Gets changes between two versions
        /// </summary>
        public async Task<DocumentationChanges> GetVersionChangesAsync(string fromVersion, string toVersion)
        {
            return await _versionManager.GetChangesAsync(fromVersion, toVersion);
        }

        private async Task<DocumentationSet> LoadCurrentDocumentationAsync()
        {
            // Load current documentation files
            var docs = new DocumentationSet();
            
            // Add implementation to load documentation files
            // This would involve scanning the docs directory and loading all relevant files

            return docs;
        }
    }

    /// <summary>
    /// Options for documentation generation
    /// </summary>
    public class GenerationOptions
    {
        public bool IncludePlugins { get; set; } = true;
        public bool IncludeValidation { get; set; } = true;
        public bool CreateVersion { get; set; } = true;
        public bool ValidateDocumentation { get; set; } = true;
        public string Version { get; set; }
        public string VersionDescription { get; set; }
    }
}