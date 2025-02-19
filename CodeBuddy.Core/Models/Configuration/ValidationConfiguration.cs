using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Configuration for validation rules and behaviors
    /// </summary>
    [SchemaVersion("2.0")]
    public class ValidationConfiguration : BaseConfiguration
    {
        [Required]
        [Range(1, 10)]
        public int MaxConcurrentValidations { get; set; } = 4;

        [Required]
        [MinLength(1)]
        public List<string> EnabledValidators { get; set; } = new();

        [Required]
        [Range(0, int.MaxValue)]
        public int ValidationTimeoutMs { get; set; } = 30000;

        [EnvironmentSpecific("Development", "Staging", "Production")]
        public Dictionary<string, int> ResourceLimits { get; set; } = new();

        [Reloadable]
        public bool EnableDetailedLogging { get; set; }

        [SensitiveData]
        public string? ValidationApiKey { get; set; }

        public ValidationCacheSettings CacheSettings { get; set; } = new();

        public override ValidationResult? Validate()
        {
            var baseResult = base.Validate();
            if (baseResult?.ValidationResult != ValidationResult.Success)
            {
                return baseResult;
            }

            // Custom validation logic
            if (EnabledValidators.Count > MaxConcurrentValidations)
            {
                return new ValidationResult(
                    "Number of enabled validators cannot exceed MaxConcurrentValidations");
            }

            // Validate resource limits
            foreach (var limit in ResourceLimits)
            {
                if (limit.Value <= 0)
                {
                    return new ValidationResult(
                        $"Resource limit for {limit.Key} must be greater than 0");
                }
            }

            return ValidationResult.Success;
        }
    }

    public class ValidationCacheSettings
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int MaxCacheSize { get; set; } = 1000;

        [Required]
        [Range(1, int.MaxValue)]
        public int CacheExpirationMinutes { get; set; } = 60;

        public bool EnableCache { get; set; } = true;
    }
}