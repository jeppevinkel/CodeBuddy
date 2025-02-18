using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Provides CI/CD pipeline integration for documentation validation
    /// </summary>
    public class DocumentationPipelineHook
    {
        private readonly DocumentationValidator _validator;
        private readonly DocumentationGenerator _generator;
        private readonly DocumentationAPI _api;

        public DocumentationPipelineHook(
            DocumentationValidator validator,
            DocumentationGenerator generator,
            DocumentationAPI api)
        {
            _validator = validator;
            _generator = generator;
            _api = api;
        }

        /// <summary>
        /// Executes documentation validation as part of the CI/CD pipeline
        /// </summary>
        public async Task<bool> ExecutePipelineValidationAsync(string workspacePath, ValidationOptions options)
        {
            try
            {
                // Generate documentation
                var documentation = await _generator.GenerateAsync(workspacePath);

                // Validate against standards
                var validationResult = await _validator.ValidateAgainstStandardsAsync(documentation);

                // Report results through API
                await _api.ReportValidationResultAsync(validationResult);

                // Fail the pipeline if there are critical issues
                if (validationResult.Issues.Exists(i => i.Severity == IssueSeverity.Critical))
                {
                    return false;
                }

                // Fail if quality score is below threshold
                if (options.EnforceQualityScore && validationResult.QualityScore < options.MinimumQualityScore)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                await _api.ReportValidationErrorAsync(ex);
                return false;
            }
        }
    }
}