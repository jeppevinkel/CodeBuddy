using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    public class ValidatorDiscoveryConfiguration
    {
        public string[] ValidatorPaths { get; set; } = new[] { "Validators" };
        public string[] AssemblyScanPatterns { get; set; } = new[] { "*.Validators.dll", "*.CodeValidation.dll" };
        public bool IncludeSubdirectories { get; set; } = true;
        public bool EnableHotReload { get; set; } = true;
        public int HotReloadDebounceMs { get; set; } = 500;
        
        public ValidatorLoadingPreferences LoadingPreferences { get; set; } = new ValidatorLoadingPreferences();
        public VersionCompatibilityRules VersionRules { get; set; } = new VersionCompatibilityRules();
        public ResourceAllocationLimits ResourceLimits { get; set; } = new ResourceAllocationLimits();
    }

    public class ValidatorLoadingPreferences
    {
        public bool PreferHighPerformance { get; set; }
        public bool PreferLowMemory { get; set; }
        public bool EnableConcurrentLoading { get; set; } = true;
        public string[] ValidatorBlacklist { get; set; } = new string[0];
        public string[] ValidatorWhitelist { get; set; } = new string[0];
        public Dictionary<string, int> ValidatorPriorities { get; set; } = new Dictionary<string, int>();
    }

    public class VersionCompatibilityRules
    {
        public bool StrictVersioning { get; set; } = false;
        public bool AllowPrerelease { get; set; } = false;
        public Dictionary<string, string[]> LanguageVersionMappings { get; set; } = new Dictionary<string, string[]>();
        public string[] ForcedFallbackVersions { get; set; } = new string[0];
        public bool AutomaticUpgrade { get; set; } = true;
    }

    public class ResourceAllocationLimits
    {
        public int MaxConcurrentValidators { get; set; } = 5;
        public int MaxMemoryPerValidatorMB { get; set; } = 512;
        public int MaxTotalMemoryMB { get; set; } = 2048;
        public int ValidationTimeoutSeconds { get; set; } = 30;
        public bool EnableResourceMonitoring { get; set; } = true;
    }
}