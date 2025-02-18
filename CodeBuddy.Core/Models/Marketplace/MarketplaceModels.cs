using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Marketplace
{
    public class MarketplacePlugin
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public Version Version { get; set; }
        public string Homepage { get; set; }
        public string Repository { get; set; }
        public List<string> Tags { get; set; }
        public double Rating { get; set; }
        public int DownloadCount { get; set; }
        public DateTime PublishedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<PluginDependency> Dependencies { get; set; }
        public VersionRange CodeBuddyCompatibility { get; set; }
    }

    public class PluginSearchCriteria
    {
        public string Keyword { get; set; }
        public string Tag { get; set; }
        public string Author { get; set; }
        public bool? HasVerifiedSignature { get; set; }
        public double? MinimumRating { get; set; }
        public VersionRange CodeBuddyVersion { get; set; }
    }

    public class PluginInstallationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public MarketplacePlugin Plugin { get; set; }
        public List<string> Warnings { get; set; }
        public Exception Error { get; set; }
    }

    public class PluginUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Version PreviousVersion { get; set; }
        public Version NewVersion { get; set; }
        public List<string> Changes { get; set; }
        public Exception Error { get; set; }
    }

    public class PluginRating
    {
        public int Score { get; set; }
        public string Review { get; set; }
        public string UserId { get; set; }
        public Version PluginVersion { get; set; }
        public DateTime SubmissionDate { get; set; }
    }

    public class PluginCompatibilityResult
    {
        public bool IsCompatible { get; set; }
        public string Message { get; set; }
        public List<string> IncompatibilityReasons { get; set; }
        public List<VersionRange> CompatibleVersions { get; set; }
    }

    public class PluginVerificationResult
    {
        public bool IsVerified { get; set; }
        public string SignatureStatus { get; set; }
        public string PublisherInfo { get; set; }
        public DateTime SignatureDate { get; set; }
        public List<string> SecurityWarnings { get; set; }
    }

    public class VersionRange
    {
        public Version MinVersion { get; set; }
        public Version MaxVersion { get; set; }
        public bool IsMinInclusive { get; set; }
        public bool IsMaxInclusive { get; set; }
    }

    public class PluginDependency
    {
        public string PluginId { get; set; }
        public VersionRange VersionRange { get; set; }
        public bool IsOptional { get; set; }
    }
}