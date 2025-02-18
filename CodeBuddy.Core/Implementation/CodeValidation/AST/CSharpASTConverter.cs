using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    public class CSharpASTConverter : IASTConverter
    {
        public string Language => "C#";

        public bool CanHandle(string sourceCode)
        {
            try
            {
                SyntaxFactory.ParseCompilationUnit(sourceCode);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UnifiedASTNode> ConvertToUnifiedASTAsync(string sourceCode, string filePath = null)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = await syntaxTree.GetRootAsync();
            
            return ConvertNode(root, filePath);
        }

        private UnifiedASTNode ConvertNode(SyntaxNode node, string filePath)
        {
            var unifiedNode = new UnifiedASTNode
            {
                NodeType = node.Kind().ToString(),
                Name = GetNodeName(node),
                SourceLanguage = Language,
                Location = GetSourceLocation(node, filePath)
            };

            foreach (var child in node.ChildNodes())
            {
                unifiedNode.Children.Add(ConvertNode(child, filePath));
            }

            AddNodeSpecificProperties(node, unifiedNode);

            return unifiedNode;
        }

        private string GetNodeName(SyntaxNode node)
        {
            // Extract name based on node type
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classNode)
                return classNode.Identifier.Text;
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodNode)
                return methodNode.Identifier.Text;
            // Add more node types as needed
            
            return string.Empty;
        }

        private SourceLocation GetSourceLocation(SyntaxNode node, string filePath)
        {
            var span = node.GetLocation().GetLineSpan();
            return new SourceLocation
            {
                StartLine = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                StartColumn = span.StartLinePosition.Character + 1,
                EndColumn = span.EndLinePosition.Character + 1,
                FilePath = filePath
            };
        }

        private void AddNodeSpecificProperties(SyntaxNode node, UnifiedASTNode unifiedNode)
        {
            switch (node)
            {
                case Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classNode:
                    unifiedNode.Properties["Modifiers"] = string.Join(" ", classNode.Modifiers);
                    unifiedNode.Properties["BaseTypes"] = string.Join(",", classNode.BaseList?.Types ?? Array.Empty<object>());
                    break;

                case Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodNode:
                    unifiedNode.Properties["ReturnType"] = methodNode.ReturnType.ToString();
                    unifiedNode.Properties["Modifiers"] = string.Join(" ", methodNode.Modifiers);
                    break;

                // Add more cases for other node types
            }
        }
    }
}