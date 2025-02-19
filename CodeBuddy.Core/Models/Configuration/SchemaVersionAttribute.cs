using System;

namespace CodeBuddy.Core.Models.Configuration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SchemaVersionAttribute : Attribute
    {
        public Version Version { get; }

        public SchemaVersionAttribute(string version)
        {
            Version = Version.Parse(version);
        }
    }
}