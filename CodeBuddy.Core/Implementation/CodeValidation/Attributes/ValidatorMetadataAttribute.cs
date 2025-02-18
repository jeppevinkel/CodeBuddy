using System;

namespace CodeBuddy.Core.Implementation.CodeValidation.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ValidatorMetadataAttribute : Attribute
    {
        public string[] SupportedLanguageVersions { get; }
        public string[] RequiredDependencies { get; }
        public string[] ValidationCapabilities { get; }
        public PerformanceProfile PerformanceProfile { get; }
        public ResourceRequirements ResourceRequirements { get; }

        public ValidatorMetadataAttribute(
            string[] supportedLanguageVersions,
            string[] requiredDependencies = null,
            string[] validationCapabilities = null,
            PerformanceProfile performanceProfile = PerformanceProfile.Normal,
            ResourceRequirements resourceRequirements = ResourceRequirements.Low)
        {
            SupportedLanguageVersions = supportedLanguageVersions;
            RequiredDependencies = requiredDependencies ?? Array.Empty<string>();
            ValidationCapabilities = validationCapabilities ?? Array.Empty<string>();
            PerformanceProfile = performanceProfile;
            ResourceRequirements = resourceRequirements;
        }
    }

    public enum PerformanceProfile
    {
        Light,
        Normal,
        Intensive
    }

    public enum ResourceRequirements
    {
        Low,
        Medium,
        High
    }
}