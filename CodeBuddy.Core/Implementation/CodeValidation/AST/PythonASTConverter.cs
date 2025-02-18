using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Core.Implementation.CodeValidation.AST
{
    public class PythonASTConverter : IASTConverter
    {
        private readonly ScriptEngine _engine;

        public PythonASTConverter()
        {
            _engine = Python.CreateEngine();
        }

        public string Language => "Python";

        public bool CanHandle(string sourceCode)
        {
            try
            {
                var source = _engine.CreateScriptSourceFromString(sourceCode);
                source.Compile();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UnifiedASTNode> ConvertToUnifiedASTAsync(string sourceCode, string filePath = null)
        {
            var source = _engine.CreateScriptSourceFromString(sourceCode);
            var ast = source.ParseAndBindAndCompile();
            
            return await Task.FromResult(ConvertNode(ast, filePath));
        }

        private UnifiedASTNode ConvertNode(dynamic node, string filePath)
        {
            var unifiedNode = new UnifiedASTNode
            {
                NodeType = GetNodeType(node),
                Name = GetNodeName(node),
                SourceLanguage = Language,
                Location = GetSourceLocation(node, filePath)
            };

            AddNodeSpecificProperties(node, unifiedNode);
            AddChildNodes(node, unifiedNode, filePath);

            return unifiedNode;
        }

        private string GetNodeType(dynamic node)
        {
            // Get the Python type name and map it to our unified type system
            string pythonType = node.GetType().Name;
            switch (pythonType)
            {
                case "FunctionDefinition":
                    return "Function";
                case "ClassDefinition":
                    return "Class";
                case "AssignmentStatement":
                    return "Assignment";
                // Add more mappings as needed
                default:
                    return pythonType;
            }
        }

        private string GetNodeName(dynamic node)
        {
            try
            {
                if (node.Name != null)
                    return node.Name.ToString();
            }
            catch {}

            return string.Empty;
        }

        private SourceLocation GetSourceLocation(dynamic node, string filePath)
        {
            try
            {
                return new SourceLocation
                {
                    StartLine = node.Start.Line,
                    EndLine = node.End.Line,
                    StartColumn = node.Start.Column,
                    EndColumn = node.End.Column,
                    FilePath = filePath
                };
            }
            catch
            {
                return new SourceLocation
                {
                    FilePath = filePath
                };
            }
        }

        private void AddNodeSpecificProperties(dynamic node, UnifiedASTNode unifiedNode)
        {
            try
            {
                switch (GetNodeType(node))
                {
                    case "Function":
                        unifiedNode.Properties["Parameters"] = string.Join(",", GetFunctionParameters(node));
                        unifiedNode.Properties["IsAsync"] = IsAsyncFunction(node);
                        break;

                    case "Class":
                        unifiedNode.Properties["BaseClasses"] = string.Join(",", GetBaseClasses(node));
                        break;

                    // Add more cases as needed
                }
            }
            catch {}
        }

        private void AddChildNodes(dynamic node, UnifiedASTNode unifiedNode, string filePath)
        {
            try
            {
                var body = node.Body as dynamic;
                if (body != null)
                {
                    foreach (var child in body)
                    {
                        unifiedNode.Children.Add(ConvertNode(child, filePath));
                    }
                }
            }
            catch {}
        }

        private IEnumerable<string> GetFunctionParameters(dynamic function)
        {
            var parameters = new List<string>();
            try
            {
                foreach (var param in function.Parameters)
                {
                    parameters.Add(param.Name);
                }
            }
            catch {}
            return parameters;
        }

        private bool IsAsyncFunction(dynamic function)
        {
            try
            {
                return function.IsAsync;
            }
            catch
            {
                return false;
            }
        }

        private IEnumerable<string> GetBaseClasses(dynamic classNode)
        {
            var bases = new List<string>();
            try
            {
                foreach (var baseClass in classNode.Bases)
                {
                    bases.Add(baseClass.Name);
                }
            }
            catch {}
            return bases;
        }
    }
}