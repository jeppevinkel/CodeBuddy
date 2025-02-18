using System;
using System.Text.RegularExpressions;

namespace CodeBuddy.Core.Models.Configuration
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class SchemaVersionAttribute : Attribute
    {
        private static readonly Regex SemVerPattern = new Regex(
            @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
            RegexOptions.Compiled);

        public string Version { get; }
        public string Description { get; }

        public SchemaVersionAttribute(string version, string description = "")
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version cannot be null or empty", nameof(version));

            if (!SemVerPattern.IsMatch(version))
                throw new ArgumentException("Version must follow semantic versioning format (e.g. 1.0.0)", nameof(version));

            Version = version;
            Description = description;
        }
    }
}