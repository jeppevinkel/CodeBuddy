using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Engine for matching patterns in the unified AST
    /// </summary>
    public class ASTPatternMatcher
    {
        /// <summary>
        /// Represents a pattern to match in the AST
        /// </summary>
        public class ASTPattern
        {
            public string NodeType { get; set; }
            public Dictionary<string, object> Constraints { get; set; }
            public List<ASTPattern> ChildPatterns { get; set; }
            
            public ASTPattern()
            {
                Constraints = new Dictionary<string, object>();
                ChildPatterns = new List<ASTPattern>();
            }
        }

        /// <summary>
        /// Finds all nodes in the AST that match the given pattern
        /// </summary>
        public async Task<IEnumerable<UnifiedASTNode>> FindMatchesAsync(UnifiedASTNode root, ASTPattern pattern)
        {
            var matches = new List<UnifiedASTNode>();
            await MatchNodeRecursiveAsync(root, pattern, matches);
            return matches;
        }

        private async Task MatchNodeRecursiveAsync(UnifiedASTNode node, ASTPattern pattern, List<UnifiedASTNode> matches)
        {
            if (MatchesPattern(node, pattern))
            {
                matches.Add(node);
            }

            foreach (var child in node.Children)
            {
                await MatchNodeRecursiveAsync(child, pattern, matches);
            }
        }

        private bool MatchesPattern(UnifiedASTNode node, ASTPattern pattern)
        {
            if (node.NodeType != pattern.NodeType)
                return false;

            foreach (var constraint in pattern.Constraints)
            {
                if (!node.Properties.ContainsKey(constraint.Key) || 
                    !node.Properties[constraint.Key].Equals(constraint.Value))
                {
                    return false;
                }
            }

            if (pattern.ChildPatterns.Count > 0)
            {
                if (pattern.ChildPatterns.Count > node.Children.Count)
                    return false;

                for (int i = 0; i < pattern.ChildPatterns.Count; i++)
                {
                    if (!MatchesPattern(node.Children[i], pattern.ChildPatterns[i]))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a new pattern for matching specific code structures
        /// </summary>
        public ASTPattern CreatePattern(string nodeType, Dictionary<string, object> constraints = null, List<ASTPattern> childPatterns = null)
        {
            return new ASTPattern
            {
                NodeType = nodeType,
                Constraints = constraints ?? new Dictionary<string, object>(),
                ChildPatterns = childPatterns ?? new List<ASTPattern>()
            };
        }
    }
}