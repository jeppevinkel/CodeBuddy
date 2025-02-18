using System;
using System.Collections.Generic;
using System.Linq;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    /// <summary>
    /// Implements semantic validation and analysis for AST nodes
    /// </summary>
    public class ASTSemanticValidator : IASTValidator
    {
        private readonly ASTValidationRegistry _validationRegistry;

        public ASTSemanticValidator(ASTValidationRegistry validationRegistry)
        {
            _validationRegistry = validationRegistry;
        }

        public IEnumerable<ASTValidationError> ValidateNode(UnifiedASTNode node)
        {
            var errors = new List<ASTValidationError>();

            // Get type rules for the node's language
            var typeRules = _validationRegistry.GetTypeRules(node.SourceLanguage, node.NodeType);
            if (typeRules == null)
            {
                errors.Add(new ASTValidationError
                {
                    ErrorCode = "AST001",
                    Message = $"Unknown node type '{node.NodeType}' for language '{node.SourceLanguage}'",
                    Severity = ErrorSeverity.Error,
                    Location = node.Location
                });
                return errors;
            }

            // Validate properties
            foreach (var property in node.Properties)
            {
                if (!typeRules.AllowedProperties.TryGetValue(property.Key, out var expectedType))
                {
                    errors.Add(new ASTValidationError
                    {
                        ErrorCode = "AST002",
                        Message = $"Invalid property '{property.Key}' for node type '{node.NodeType}'",
                        Severity = ErrorSeverity.Warning,
                        Location = node.Location
                    });
                    continue;
                }

                if (!ValidatePropertyType(property.Value, expectedType))
                {
                    errors.Add(new ASTValidationError
                    {
                        ErrorCode = "AST003",
                        Message = $"Invalid type for property '{property.Key}'. Expected {expectedType}",
                        Severity = ErrorSeverity.Error,
                        Location = node.Location
                    });
                }
            }

            // Validate child node types
            foreach (var child in node.Children)
            {
                if (!typeRules.ValidChildTypes.Contains(child.NodeType))
                {
                    errors.Add(new ASTValidationError
                    {
                        ErrorCode = "AST004",
                        Message = $"Invalid child node type '{child.NodeType}' for parent type '{node.NodeType}'",
                        Severity = ErrorSeverity.Error,
                        Location = child.Location
                    });
                }

                // Recursively validate child nodes
                errors.AddRange(ValidateNode(child));
            }

            return errors;
        }

        public SemanticAnalysisResult AnalyzeSemantics(UnifiedASTNode node, SemanticContext context)
        {
            var result = new SemanticAnalysisResult();
            
            // Create a new scope for the current node if needed
            if (IsNodeWithScope(node.NodeType))
            {
                var scope = new ScopeContext
                {
                    ScopeType = node.NodeType,
                    ScopeNode = node
                };
                context.ScopeStack.Push(scope);
            }

            try
            {
                // Process declarations
                if (IsDeclarationNode(node))
                {
                    ProcessDeclaration(node, context);
                }

                // Analyze node semantics
                var semanticErrors = AnalyzeNodeSemantics(node, context);
                result.Errors.AddRange(semanticErrors);

                // Recursively analyze child nodes
                foreach (var child in node.Children)
                {
                    var childResult = AnalyzeSemantics(child, context);
                    result.Errors.AddRange(childResult.Errors);
                    
                    // Merge analysis metadata
                    foreach (var metadata in childResult.AnalysisMetadata)
                    {
                        result.AnalysisMetadata[metadata.Key] = metadata.Value;
                    }
                }
            }
            finally
            {
                // Pop the scope if we created one
                if (IsNodeWithScope(node.NodeType))
                {
                    context.ScopeStack.Pop();
                }
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public IEnumerable<ASTValidationError> ValidateRelationships(IEnumerable<UnifiedASTNode> nodes)
        {
            var errors = new List<ASTValidationError>();
            var context = new SemanticContext();

            // First pass: collect all declarations
            foreach (var node in nodes)
            {
                if (IsDeclarationNode(node))
                {
                    ProcessDeclaration(node, context);
                }
            }

            // Second pass: validate relationships
            foreach (var node in nodes)
            {
                var rules = _validationRegistry.GetRelationshipRules(node.SourceLanguage);
                foreach (var rule in rules.Where(r => r.SourceType == node.NodeType))
                {
                    // Find related nodes based on the rule
                    var relatedNodes = FindRelatedNodes(nodes, node, rule.TargetType);
                    foreach (var relatedNode in relatedNodes)
                    {
                        if (!rule.ValidationLogic(node, relatedNode, context))
                        {
                            errors.Add(new ASTValidationError
                            {
                                ErrorCode = "REL001",
                                Message = $"Invalid relationship between {node.NodeType} '{node.Name}' and {relatedNode.NodeType} '{relatedNode.Name}'",
                                Severity = ErrorSeverity.Error,
                                Location = node.Location
                            });
                        }
                    }
                }
            }

            return errors;
        }

        private bool ValidatePropertyType(object value, string expectedType)
        {
            return expectedType.ToLower() switch
            {
                "string" => value is string,
                "bool" => value is bool,
                "int" => value is int,
                "string[]" => value is string[],
                _ => false
            };
        }

        private bool IsNodeWithScope(string nodeType)
        {
            return new[] { "Class", "Method", "Function", "Block", "Namespace" }.Contains(nodeType);
        }

        private bool IsDeclarationNode(UnifiedASTNode node)
        {
            return new[] { "Class", "Method", "Variable", "Function", "Interface" }.Contains(node.NodeType);
        }

        private void ProcessDeclaration(UnifiedASTNode node, SemanticContext context)
        {
            if (context.ScopeStack.Count > 0)
            {
                context.ScopeStack.Peek().LocalDeclarations[node.Name] = node;
            }
            else
            {
                context.Declarations[node.Name] = node;
            }
        }

        private IEnumerable<ASTValidationError> AnalyzeNodeSemantics(UnifiedASTNode node, SemanticContext context)
        {
            var errors = new List<ASTValidationError>();

            switch (node.NodeType)
            {
                case "VariableReference":
                    if (!IsVariableDeclared(node.Name, context))
                    {
                        errors.Add(new ASTValidationError
                        {
                            ErrorCode = "SEM001",
                            Message = $"Reference to undeclared variable '{node.Name}'",
                            Severity = ErrorSeverity.Error,
                            Location = node.Location
                        });
                    }
                    break;

                case "MethodCall":
                    if (!IsMethodDeclared(node.Name, context))
                    {
                        errors.Add(new ASTValidationError
                        {
                            ErrorCode = "SEM002",
                            Message = $"Call to undeclared method '{node.Name}'",
                            Severity = ErrorSeverity.Error,
                            Location = node.Location
                        });
                    }
                    break;

                // Add more semantic checks for other node types...
            }

            return errors;
        }

        private bool IsVariableDeclared(string name, SemanticContext context)
        {
            return context.Declarations.ContainsKey(name) ||
                   context.ScopeStack.Any(s => s.LocalDeclarations.ContainsKey(name));
        }

        private bool IsMethodDeclared(string name, SemanticContext context)
        {
            return context.Declarations.ContainsKey(name) ||
                   context.ScopeStack.Any(s => s.LocalDeclarations.ContainsKey(name));
        }

        private IEnumerable<UnifiedASTNode> FindRelatedNodes(
            IEnumerable<UnifiedASTNode> allNodes,
            UnifiedASTNode sourceNode,
            string targetType)
        {
            return allNodes.Where(n => n.NodeType == targetType &&
                                     IsNodeRelated(sourceNode, n));
        }

        private bool IsNodeRelated(UnifiedASTNode source, UnifiedASTNode target)
        {
            // Implementation depends on the specific relationship types needed
            // This is a basic implementation that can be extended
            return source.Name == target.Name ||
                   source.Children.Any(c => c.Name == target.Name) ||
                   target.Children.Any(c => c.Name == source.Name);
        }
    }
}