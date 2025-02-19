using System;

namespace CodeBuddy.Core.Models.Configuration
{
    /// <summary>
    /// Specifies the schema version for a configuration class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class SchemaVersionAttribute : Attribute
    {
        public Version Version { get; }

        public SchemaVersionAttribute(string version)
        {
            Version = Version.Parse(version);
        }
    }

    /// <summary>
    /// Marks a class as a configuration section
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ConfigurationSectionAttribute : Attribute
    {
        public string Name { get; }
        public Version Version { get; }

        public ConfigurationSectionAttribute(string name, string version = "1.0")
        {
            Name = name;
            Version = Version.Parse(version);
        }
    }
}