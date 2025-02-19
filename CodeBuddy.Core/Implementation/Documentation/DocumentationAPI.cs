using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Main API for documentation generation and management
    /// </summary>
    public class DocumentationAPI
    {
        private readonly IDocumentationGenerator _generator;
        private readonly DocumentationValidator _validator;
        private readonly DocumentationVersionManager _versionManager;
        
        public DocumentationAPI(
            IDocumentationGenerator generator,
            DocumentationValidator validator,
            DocumentationVersionManager versionManager)
        {
            _generator = generator;
            _validator = validator;
            _versionManager = versionManager;
        }
        
        /// <summary>
        /// Generates comprehensive documentation based on the provided options
        /// </summary>
        public async Task<DocumentationResult> GenerateDocumentationAsync(GenerationOptions options)
        {
            var result = new DocumentationResult();
            
            try
            {
                // Generate API documentation
                var apiResult = await _generator.GenerateApiDocumentationAsync();
                if (!apiResult.Success)
                {
                    return apiResult;
                }
                
                // Generate feature documentation
                var featureResult = await _generator.GenerateFeatureDocumentationAsync();
                if (!featureResult.Success)
                {
                    return featureResult;
                }
                
                // Generate plugin documentation
                var pluginResult = await _generator.GeneratePluginDocumentationAsync();
                if (!pluginResult.Success)
                {
                    return pluginResult;
                }
                
                // Generate validation documentation
                var validationResult = await _generator.GenerateValidationDocumentationAsync();
                if (!validationResult.Success)
                {
                    return validationResult;
                }
                
                // Generate configuration documentation
                var configResult = await _generator.GenerateConfigurationDocumentationAsync();
                if (!configResult.Success)
                {
                    return configResult;
                }
                
                // Optional: Generate TypeScript definitions
                if (options.GenerateTypeScriptTypes)
                {
                    var typescriptResult = await _generator.GenerateTypeScriptDefinitionsAsync();
                    if (!typescriptResult.Success)
                    {
                        return typescriptResult;
                    }
                }
                
                // Optional: Validate documentation
                if (options.ValidateDocumentation)
                {
                    var validationResult = await _generator.ValidateDocumentationAsync();
                    result.ValidationResult = validationResult;
                    
                    if (validationResult.Coverage < options.ValidationOptions.MinimumDocumentationCoverage)
                    {
                        result.Success = false;
                        result.Error = $"Documentation coverage ({validationResult.Coverage}%) is below minimum required ({options.ValidationOptions.MinimumDocumentationCoverage}%)";
                        return result;
                    }
                }
                
                // Optional: Create version
                if (options.CreateVersion)
                {
                    var version = await _versionManager.CreateVersionAsync(
                        options.Version ?? DateTime.UtcNow.ToString("yyyyMMdd.HHmmss"),
                        options.VersionDescription ?? "Generated documentation update");
                    result.Version = version;
                }
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validates existing documentation
        /// </summary>
        public async Task<DocumentationValidationResult> ValidateDocumentationAsync(ValidationOptions options)
        {
            return await _generator.ValidateDocumentationAsync();
        }
        
        /// <summary>
        /// Creates a new documentation version
        /// </summary>
        public async Task<DocumentationVersion> CreateVersionAsync(string version, string description)
        {
            return await _versionManager.CreateVersionAsync(version, description);
        }
    }
    
    public class GenerationOptions
    {
        public bool GenerateTypeScriptTypes { get; set; } = true;
        public bool GenerateDiagrams { get; set; } = true;
        public bool ValidateDocumentation { get; set; } = true;
        public bool CreateVersion { get; set; } = true;
        public string Version { get; set; }
        public string VersionDescription { get; set; }
        public ValidationOptions ValidationOptions { get; set; } = new ValidationOptions();
    }
    
    public class ValidationOptions
    {
        public int MinimumDocumentationCoverage { get; set; } = 80;
    }
}