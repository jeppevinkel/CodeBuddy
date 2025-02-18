using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models
{
    /// <summary>
    /// Represents resource usage information for an operation
    /// </summary>
    public class ResourceUsage
    {
        /// <summary>
        /// The type or name of the operation
        /// </summary>
        public string OperationType { get; set; }

        /// <summary>
        /// Memory used by the operation in bytes
        /// </summary>
        public long MemoryUsed { get; set; }

        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Number of threads used during the operation
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Number of handles used during the operation
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Additional metadata tags for the operation
        /// </summary>
        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }
}