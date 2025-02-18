using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Esprima;
using Esprima.Ast;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    public class JavaScriptASTConverter : IASTConverter
    {
        public string Language => "JavaScript";

        public bool CanHandle(string sourceCode)
        {
            try
            {
                var parser = new JavaScriptParser();
                parser.ParseScript(sourceCode);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UnifiedASTNode> ConvertToUnifiedASTAsync(string sourceCode, string filePath = null)
        {
            var parser = new JavaScriptParser();
            var program = parser.ParseScript(sourceCode);
            
            return await Task.FromResult(ConvertNode(program, filePath));
        }

        private UnifiedASTNode ConvertNode(Node node, string filePath)
        {
            var unifiedNode = new UnifiedASTNode
            {
                NodeType = node.Type.ToString(),
                Name = GetNodeName(node),
                SourceLanguage = Language,
                Location = GetSourceLocation(node, filePath)
            };

            AddNodeSpecificProperties(node, unifiedNode);
            AddChildNodes(node, unifiedNode, filePath);

            return unifiedNode;
        }

        private void AddChildNodes(Node node, UnifiedASTNode unifiedNode, string filePath)
        {
            switch (node.Type)
            {
                case Nodes.Program:
                    var program = (Program)node;
                    foreach (var child in program.Body)
                    {
                        unifiedNode.Children.Add(ConvertNode(child, filePath));
                    }
                    break;

                case Nodes.FunctionDeclaration:
                    var func = (FunctionDeclaration)node;
                    unifiedNode.Children.Add(ConvertNode(func.Body, filePath));
                    break;

                case Nodes.BlockStatement:
                    var block = (BlockStatement)node;
                    foreach (var child in block.Body)
                    {
                        unifiedNode.Children.Add(ConvertNode(child, filePath));
                    }
                    break;

                // Add more cases as needed
            }
        }

        private string GetNodeName(Node node)
        {
            switch (node.Type)
            {
                case Nodes.FunctionDeclaration:
                    return ((FunctionDeclaration)node).Id?.Name ?? string.Empty;
                case Nodes.VariableDeclarator:
                    return ((VariableDeclarator)node).Id.ToString();
                case Nodes.ClassDeclaration:
                    return ((ClassDeclaration)node).Id.Name;
                default:
                    return string.Empty;
            }
        }

        private SourceLocation GetSourceLocation(Node node, string filePath)
        {
            return new SourceLocation
            {
                StartLine = node.Location.Start.Line,
                EndLine = node.Location.End.Line,
                StartColumn = node.Location.Start.Column,
                EndColumn = node.Location.End.Column,
                FilePath = filePath
            };
        }

        private void AddNodeSpecificProperties(Node node, UnifiedASTNode unifiedNode)
        {
            switch (node.Type)
            {
                case Nodes.FunctionDeclaration:
                    var func = (FunctionDeclaration)node;
                    unifiedNode.Properties["IsGenerator"] = func.Generator;
                    unifiedNode.Properties["IsAsync"] = func.Async;
                    unifiedNode.Properties["Parameters"] = string.Join(",", func.Params);
                    break;

                case Nodes.ClassDeclaration:
                    var classDecl = (ClassDeclaration)node;
                    if (classDecl.SuperClass != null)
                        unifiedNode.Properties["SuperClass"] = classDecl.SuperClass.ToString();
                    break;

                // Add more cases as needed
            }
        }
    }
}