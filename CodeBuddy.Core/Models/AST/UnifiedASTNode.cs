using System;
using System.Collections.Generic;

namespace CodeBuddy.Core.Models.AST
{
    /// <summary>
    /// Represents a node in the unified Abstract Syntax Tree that can represent code constructs
    /// across different programming languages.
    /// </summary>
    public class UnifiedASTNode
    {
        /// <summary>
        /// The type of the node (e.g., Method, Class, Variable, etc.)
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// The name or identifier of the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The original source language this node was parsed from
        /// </summary>
        public string SourceLanguage { get; set; }

        /// <summary>
        /// Additional properties specific to the node type
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Child nodes in the AST
        /// </summary>
        public List<UnifiedASTNode> Children { get; set; }

        /// <summary>
        /// Source code location information
        /// </summary>
        public SourceLocation Location { get; set; }

        public UnifiedASTNode()
        {
            Properties = new Dictionary<string, object>();
            Children = new List<UnifiedASTNode>();
        }
    }

    /// <summary>
    /// Represents the location of a node in the source code
    /// </summary>
    public class SourceLocation
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string FilePath { get; set; }
    }
}