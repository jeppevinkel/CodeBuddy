using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.Documentation
{
    /// <summary>
    /// Represents a version of documentation
    /// </summary>
    public class DocumentationVersion
    {
        public string Version { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Files { get; set; }
    }

    /// <summary>
    /// Represents a complete archive of documentation for a version
    /// </summary>
    public class DocumentationArchive
    {
        public DocumentationVersion Version { get; set; }
        public Dictionary<string, string> Files { get; set; }
    }

    /// <summary>
    /// Represents changes between two documentation versions
    /// </summary>
    public class DocumentationChanges
    {
        public string FromVersion { get; set; }
        public string ToVersion { get; set; }
        public List<FileChange> ChangedFiles { get; set; }
    }

    /// <summary>
    /// Represents a change to a documentation file
    /// </summary>
    public class FileChange
    {
        public string File { get; set; }
        public ChangeType ChangeType { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// Type of change made to a file
    /// </summary>
    public enum ChangeType
    {
        Added,
        Modified,
        Deleted
    }

    /// <summary>
    /// Thrown when documentation operations fail
    /// </summary>
    public class DocumentationException : Exception
    {
        public DocumentationException(string message) : base(message) { }
        public DocumentationException(string message, Exception inner) : base(message, inner) { }
    }
}