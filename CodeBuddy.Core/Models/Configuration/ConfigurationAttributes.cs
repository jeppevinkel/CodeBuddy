using System;

namespace CodeBuddy.Core.Models.Configuration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SchemaVersionAttribute : Attribute
    {
        public string Version { get; }

        public SchemaVersionAttribute(string version)
        {
            Version = version;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentVariableAttribute : Attribute
    {
        public string VariableName { get; }

        public EnvironmentVariableAttribute(string variableName)
        {
            VariableName = variableName;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SecureStorageAttribute : Attribute
    {
    }
}