using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Marketplace;

namespace CodeBuddy.Core.Implementation.Marketplace
{
    public class DefaultPluginMarketplaceService : IPluginMarketplaceService
    {
        private readonly IPluginManager _pluginManager;
        private readonly IPluginAuthService _authService;
        private readonly HttpClient _httpClient;
        private readonly string _marketplaceApiUrl;

        public DefaultPluginMarketplaceService(
            IPluginManager pluginManager,
            IPluginAuthService authService,
            string marketplaceApiUrl)
        {
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _marketplaceApiUrl = marketplaceApiUrl ?? throw new ArgumentNullException(nameof(marketplaceApiUrl));
            _httpClient = new HttpClient { BaseAddress = new Uri(_marketplaceApiUrl) };
        }

        public async Task<IEnumerable<MarketplacePlugin>> QueryAvailablePluginsAsync(PluginSearchCriteria searchCriteria = null)
        {
            try
            {
                var queryString = BuildQueryString(searchCriteria);
                var response = await _httpClient.GetAsync($"/api/plugins{queryString}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsAsync<List<MarketplacePlugin>>();
            }
            catch (Exception ex)
            {
                throw new MarketplaceException("Failed to query available plugins", ex);
            }
        }

        public async Task<PluginInstallationResult> InstallPluginAsync(string pluginId, Version version = null)
        {
            try
            {
                // Verify plugin compatibility
                var compatibilityResult = await CheckPluginCompatibilityAsync(pluginId);
                if (!compatibilityResult.IsCompatible)
                {
                    return new PluginInstallationResult
                    {
                        Success = false,
                        Message = "Plugin is not compatible with current CodeBuddy version",
                        Warnings = compatibilityResult.IncompatibilityReasons
                    };
                }

                // Download plugin package
                var packageUrl = $"/api/plugins/{pluginId}/download";
                if (version != null)
                {
                    packageUrl += $"?version={version}";
                }
                
                var packageResponse = await _httpClient.GetAsync(packageUrl);
                packageResponse.EnsureSuccessStatusCode();
                var packageBytes = await packageResponse.Content.ReadAsByteArrayAsync();

                // Verify signature
                var verificationResult = await VerifyPluginSignatureAsync(pluginId);
                if (!verificationResult.IsVerified)
                {
                    return new PluginInstallationResult
                    {
                        Success = false,
                        Message = "Plugin signature verification failed",
                        Warnings = verificationResult.SecurityWarnings
                    };
                }

                // Install plugin through plugin manager
                var plugin = await _pluginManager.InstallPluginAsync(packageBytes);

                return new PluginInstallationResult
                {
                    Success = true,
                    Message = "Plugin installed successfully",
                    Plugin = new MarketplacePlugin
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Version = plugin.Version
                    }
                };
            }
            catch (Exception ex)
            {
                return new PluginInstallationResult
                {
                    Success = false,
                    Message = "Failed to install plugin",
                    Error = ex
                };
            }
        }

        public async Task<PluginUpdateResult> UpdatePluginAsync(string pluginId)
        {
            try
            {
                var currentPlugin = await _pluginManager.GetPluginAsync(pluginId);
                if (currentPlugin == null)
                {
                    throw new PluginNotFoundException($"Plugin {pluginId} is not installed");
                }

                var latestVersion = await GetLatestVersionAsync(pluginId);
                if (latestVersion <= currentPlugin.Version)
                {
                    return new PluginUpdateResult
                    {
                        Success = true,
                        Message = "Plugin is already up to date",
                        PreviousVersion = currentPlugin.Version,
                        NewVersion = currentPlugin.Version
                    };
                }

                var installResult = await InstallPluginAsync(pluginId, latestVersion);
                if (!installResult.Success)
                {
                    return new PluginUpdateResult
                    {
                        Success = false,
                        Message = installResult.Message,
                        PreviousVersion = currentPlugin.Version,
                        Error = installResult.Error
                    };
                }

                return new PluginUpdateResult
                {
                    Success = true,
                    Message = "Plugin updated successfully",
                    PreviousVersion = currentPlugin.Version,
                    NewVersion = latestVersion,
                    Changes = await GetChangelogAsync(pluginId, currentPlugin.Version, latestVersion)
                };
            }
            catch (Exception ex)
            {
                return new PluginUpdateResult
                {
                    Success = false,
                    Message = "Failed to update plugin",
                    Error = ex
                };
            }
        }

        public async Task<bool> SubmitPluginRatingAsync(string pluginId, PluginRating rating)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/plugins/{pluginId}/ratings", rating);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<PluginCompatibilityResult> CheckPluginCompatibilityAsync(string pluginId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/plugins/{pluginId}/compatibility");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsAsync<PluginCompatibilityResult>();
            }
            catch (Exception ex)
            {
                throw new MarketplaceException("Failed to check plugin compatibility", ex);
            }
        }

        public async Task<PluginVerificationResult> VerifyPluginSignatureAsync(string pluginId)
        {
            try
            {
                var pluginMetadata = await _httpClient.GetAsync($"/api/plugins/{pluginId}/metadata");
                pluginMetadata.EnsureSuccessStatusCode();
                var metadata = await pluginMetadata.Content.ReadAsAsync<PluginMetadata>();

                // Verify signature using auth service
                var signatureValid = await _authService.VerifyPluginSignatureAsync(
                    metadata.SignatureData,
                    metadata.PublicKey,
                    metadata.Signature);

                return new PluginVerificationResult
                {
                    IsVerified = signatureValid,
                    SignatureStatus = signatureValid ? "Valid" : "Invalid",
                    PublisherInfo = metadata.PublisherInfo,
                    SignatureDate = metadata.SignatureDate,
                    SecurityWarnings = signatureValid ? new List<string>() : new List<string> { "Plugin signature verification failed" }
                };
            }
            catch (Exception ex)
            {
                throw new MarketplaceException("Failed to verify plugin signature", ex);
            }
        }

        private string BuildQueryString(PluginSearchCriteria criteria)
        {
            if (criteria == null) return string.Empty;

            var queryParams = new List<string>();
            
            if (!string.IsNullOrEmpty(criteria.Keyword))
                queryParams.Add($"keyword={Uri.EscapeDataString(criteria.Keyword)}");
            
            if (!string.IsNullOrEmpty(criteria.Tag))
                queryParams.Add($"tag={Uri.EscapeDataString(criteria.Tag)}");
            
            if (!string.IsNullOrEmpty(criteria.Author))
                queryParams.Add($"author={Uri.EscapeDataString(criteria.Author)}");
            
            if (criteria.HasVerifiedSignature.HasValue)
                queryParams.Add($"verified={criteria.HasVerifiedSignature.Value}");
            
            if (criteria.MinimumRating.HasValue)
                queryParams.Add($"minrating={criteria.MinimumRating.Value}");

            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        private async Task<Version> GetLatestVersionAsync(string pluginId)
        {
            var response = await _httpClient.GetAsync($"/api/plugins/{pluginId}/latest-version");
            response.EnsureSuccessStatusCode();
            var versionString = await response.Content.ReadAsStringAsync();
            return Version.Parse(versionString);
        }

        private async Task<List<string>> GetChangelogAsync(string pluginId, Version fromVersion, Version toVersion)
        {
            var response = await _httpClient.GetAsync(
                $"/api/plugins/{pluginId}/changelog?from={fromVersion}&to={toVersion}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<List<string>>();
        }
    }

    internal class PluginMetadata
    {
        public byte[] SignatureData { get; set; }
        public string PublicKey { get; set; }
        public string Signature { get; set; }
        public string PublisherInfo { get; set; }
        public DateTime SignatureDate { get; set; }
    }

    public class MarketplaceException : Exception
    {
        public MarketplaceException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    public class PluginNotFoundException : Exception
    {
        public PluginNotFoundException(string message)
            : base(message)
        {
        }
    }
}