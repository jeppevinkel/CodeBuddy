using System;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Base configuration class for plugins with stability and resource management
    /// </summary>
    [SchemaVersion("1.0")]
    public class PluginConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Maximum memory usage allowed in MB
        /// </summary>
        [Range(0, int.MaxValue)]
        public int MaxMemoryMB { get; set; }

        /// <summary>
        /// Maximum CPU usage percentage allowed (0-100)
        /// </summary>
        [Range(0, 100)]
        public int MaxCPUPercent { get; set; }

        /// <summary>
        /// Maximum number of concurrent operations allowed
        /// </summary>
        [Range(0, int.MaxValue)]
        public int MaxConcurrentOperations { get; set; }

        /// <summary>
        /// Plugin stability settings
        /// </summary>
        public ConfigurationStabilitySettings StabilitySettings { get; set; } = new();

        /// <summary>
        /// Whether the plugin should automatically recover from failures
        /// </summary>
        public bool AutoRecoveryEnabled { get; set; }

        /// <summary>
        /// Maximum number of recovery attempts
        /// </summary>
        [Range(0, int.MaxValue)]
        public int MaxRecoveryAttempts { get; set; }

        /// <summary>
        /// Delay between recovery attempts in seconds
        /// </summary>
        [Range(0, int.MaxValue)]
        public int RecoveryDelaySeconds { get; set; }

        /// <summary>
        /// Plugin execution timeout in seconds
        /// </summary>
        [Range(0, int.MaxValue)]
        public int ExecutionTimeoutSeconds { get; set; }

        /// <summary>
        /// Plugin initialization timeout in seconds
        /// </summary>
        [Range(0, int.MaxValue)]
        public int InitializationTimeoutSeconds { get; set; }

        /// <summary>
        /// Plugin dependencies required for operation
        /// </summary>
        public string[] Dependencies { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Plugin feature flags
        /// </summary>
        public Dictionary<string, bool> FeatureFlags { get; set; } = new();

        /// <summary>
        /// Plugin-specific secure storage key
        /// </summary>
        [SecureStorage]
        public string? SecureStorageKey { get; set; }

        public override ValidationResult? Validate()
        {
            var baseResult = base.Validate();
            if (baseResult != ValidationResult.Success)
            {
                return baseResult;
            }

            // Validate dependencies
            if (Dependencies != null && Dependencies.Any(d => string.IsNullOrWhiteSpace(d)))
            {
                return new ValidationResult("Plugin dependencies cannot be empty");
            }

            // Validate timeouts
            if (ExecutionTimeoutSeconds > 0 && ExecutionTimeoutSeconds < InitializationTimeoutSeconds)
            {
                return new ValidationResult("Execution timeout must be greater than initialization timeout");
            }

            // Validate recovery settings
            if (AutoRecoveryEnabled && MaxRecoveryAttempts <= 0)
            {
                return new ValidationResult("Maximum recovery attempts must be greater than zero when auto-recovery is enabled");
            }

            return ValidationResult.Success;
        }
    }
}