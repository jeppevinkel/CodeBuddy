using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Marketplace;

namespace CodeBuddy.Core.Interfaces
{
    public interface IPluginMarketplaceService
    {
        /// <summary>
        /// Queries available plugins from the marketplace
        /// </summary>
        /// <param name="searchCriteria">Optional search criteria to filter plugins</param>
        /// <returns>List of available plugins matching the criteria</returns>
        Task<IEnumerable<MarketplacePlugin>> QueryAvailablePluginsAsync(PluginSearchCriteria searchCriteria = null);

        /// <summary>
        /// Downloads and installs a plugin from the marketplace
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin</param>
        /// <param name="version">Optional specific version to install, defaults to latest</param>
        /// <returns>Installation result with status and details</returns>
        Task<PluginInstallationResult> InstallPluginAsync(string pluginId, Version version = null);

        /// <summary>
        /// Updates an installed plugin to the latest version
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin</param>
        /// <returns>Update result with status and details</returns>
        Task<PluginUpdateResult> UpdatePluginAsync(string pluginId);

        /// <summary>
        /// Submits a rating and review for a plugin
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin</param>
        /// <param name="rating">Rating details including score and review text</param>
        /// <returns>Submission result</returns>
        Task<bool> SubmitPluginRatingAsync(string pluginId, PluginRating rating);

        /// <summary>
        /// Validates plugin compatibility with the current CodeBuddy version
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin</param>
        /// <returns>Compatibility check result</returns>
        Task<PluginCompatibilityResult> CheckPluginCompatibilityAsync(string pluginId);

        /// <summary>
        /// Verifies the authenticity of a plugin package
        /// </summary>
        /// <param name="pluginId">The unique identifier of the plugin</param>
        /// <returns>Verification result with signature status</returns>
        Task<PluginVerificationResult> VerifyPluginSignatureAsync(string pluginId);
    }
}