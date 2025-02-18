using System;
using System.Collections.Generic;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Tests.PatternDetection
{
    public class TestASTBuilder
    {
        public static UnifiedASTNode CreateMethodNode(string name, string visibility = "public", string returnType = "void")
        {
            return new UnifiedASTNode
            {
                Type = "method",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "visibility", visibility },
                    { "returnType", returnType }
                }
            };
        }

        public static UnifiedASTNode CreateClassNode(string name, List<UnifiedASTNode> methods = null)
        {
            return new UnifiedASTNode
            {
                Type = "class",
                Properties = new Dictionary<string, string>
                {
                    { "name", name }
                },
                Children = methods ?? new List<UnifiedASTNode>()
            };
        }

        public static UnifiedASTNode CreateFunctionNode(string name, int paramCount = 0, bool isAsync = false)
        {
            return new UnifiedASTNode
            {
                Type = "function",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "parameterCount", paramCount.ToString() },
                    { "async", isAsync.ToString().ToLower() }
                }
            };
        }

        public static UnifiedASTNode CreateIfNode(UnifiedASTNode condition, List<UnifiedASTNode> body)
        {
            return new UnifiedASTNode
            {
                Type = "if",
                Children = new List<UnifiedASTNode> { condition }.Concat(body).ToList()
            };
        }

        public static UnifiedASTNode CreateLoopNode(string type, UnifiedASTNode condition, List<UnifiedASTNode> body)
        {
            return new UnifiedASTNode
            {
                Type = type,
                Children = new List<UnifiedASTNode> { condition }.Concat(body).ToList()
            };
        }

        public static UnifiedASTNode CreateVariableNode(string name, string type, string value = null)
        {
            return new UnifiedASTNode
            {
                Type = "variable",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "type", type },
                    { "value", value }
                }
            };
        }

        public static UnifiedASTNode CreateOperatorNode(string op, UnifiedASTNode left, UnifiedASTNode right)
        {
            return new UnifiedASTNode
            {
                Type = "operator",
                Properties = new Dictionary<string, string>
                {
                    { "operator", op }
                },
                Children = new List<UnifiedASTNode> { left, right }
            };
        }

        public static UnifiedASTNode CreateFieldNode(string name, string type, string visibility = "private")
        {
            return new UnifiedASTNode
            {
                Type = "field",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "type", type },
                    { "visibility", visibility }
                }
            };
        }

        public static UnifiedASTNode CreatePropertyNode(string name, string type, bool hasGetter = true, bool hasSetter = true)
        {
            return new UnifiedASTNode
            {
                Type = "property",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "type", type },
                    { "hasGetter", hasGetter.ToString() },
                    { "hasSetter", hasSetter.ToString() }
                }
            };
        }

        public static UnifiedASTNode CreateParameterNode(string name, string type)
        {
            return new UnifiedASTNode
            {
                Type = "parameter",
                Properties = new Dictionary<string, string>
                {
                    { "name", name },
                    { "type", type }
                }
            };
        }

        public static UnifiedASTNode CreateCompilationUnit(List<UnifiedASTNode> declarations)
        {
            return new UnifiedASTNode
            {
                Type = "compilation_unit",
                Children = declarations
            };
        }

        public static UnifiedASTNode CreateNamespaceNode(string name, List<UnifiedASTNode> members)
        {
            return new UnifiedASTNode
            {
                Type = "namespace",
                Properties = new Dictionary<string, string>
                {
                    { "name", name }
                },
                Children = members
            };
        }

        public static UnifiedASTNode CreateModuleNode(List<UnifiedASTNode> members)
        {
            return new UnifiedASTNode
            {
                Type = "module",
                Children = members
            };
        }

        public static UnifiedASTNode CreateProgramNode(List<UnifiedASTNode> statements)
        {
            return new UnifiedASTNode
            {
                Type = "program",
                Children = statements
            };
        }

        public static UnifiedASTNode CreateExpressionNode(string type, Dictionary<string, string> properties = null)
        {
            return new UnifiedASTNode
            {
                Type = "expression",
                Properties = properties ?? new Dictionary<string, string>
                {
                    { "expressionType", type }
                }
            };
        }
    }
}