using System.Linq;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Models.AST;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeBuddy.Tests.CodeValidation
{
    [TestClass]
    public class ASTSemanticValidatorTests
    {
        private ASTValidationRegistry _registry;
        private ASTSemanticValidator _validator;

        [TestInitialize]
        public void Setup()
        {
            _registry = new ASTValidationRegistry();
            _validator = new ASTSemanticValidator(_registry);
        }

        [TestMethod]
        public void ValidateNode_ValidCSharpClass_NoErrors()
        {
            // Arrange
            var node = new UnifiedASTNode(_validator)
            {
                NodeType = "Class",
                Name = "TestClass",
                SourceLanguage = "C#",
                Properties = new Dictionary<string, object>
                {
                    ["Accessibility"] = "public",
                    ["IsStatic"] = false
                }
            };

            // Act
            var errors = node.Validate().ToList();

            // Assert
            Assert.AreEqual(0, errors.Count, "No validation errors should be found for valid class node");
        }

        [TestMethod]
        public void ValidateNode_InvalidProperty_ReturnsError()
        {
            // Arrange
            var node = new UnifiedASTNode(_validator)
            {
                NodeType = "Class",
                Name = "TestClass",
                SourceLanguage = "C#",
                Properties = new Dictionary<string, object>
                {
                    ["InvalidProperty"] = "value"
                }
            };

            // Act
            var errors = node.Validate().ToList();

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "AST002"), "Should detect invalid property");
        }

        [TestMethod]
        public void AnalyzeSemantics_UndeclaredVariableReference_ReturnsError()
        {
            // Arrange
            var variableRef = new UnifiedASTNode(_validator)
            {
                NodeType = "VariableReference",
                Name = "undeclaredVar",
                SourceLanguage = "C#"
            };

            // Act
            var result = variableRef.AnalyzeSemantics();

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.ErrorCode == "SEM001"), 
                "Should detect reference to undeclared variable");
        }

        [TestMethod]
        public void ValidateRelationships_ValidVariableDeclarationAndUsage_NoErrors()
        {
            // Arrange
            var declaration = new UnifiedASTNode(_validator)
            {
                NodeType = "VariableDeclaration",
                Name = "testVar",
                SourceLanguage = "C#"
            };

            var reference = new UnifiedASTNode(_validator)
            {
                NodeType = "VariableReference",
                Name = "testVar",
                SourceLanguage = "C#"
            };

            // Act
            var context = new SemanticContext();
            declaration.SetSemanticContext(context);
            reference.SetSemanticContext(context);
            
            var result = declaration.AnalyzeSemantics();
            var errors = reference.ValidateRelationships(new[] { declaration }).ToList();

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, errors.Count, "No errors should be found for valid variable usage");
        }

        [TestMethod]
        public void AnalyzeSemantics_MethodWithValidScope_NoErrors()
        {
            // Arrange
            var method = new UnifiedASTNode(_validator)
            {
                NodeType = "Method",
                Name = "TestMethod",
                SourceLanguage = "C#",
                Properties = new Dictionary<string, object>
                {
                    ["ReturnType"] = "void",
                    ["Accessibility"] = "public"
                }
            };

            var parameter = new UnifiedASTNode(_validator)
            {
                NodeType = "Parameter",
                Name = "param1",
                Properties = new Dictionary<string, object>
                {
                    ["Type"] = "string"
                }
            };

            method.AddChild(parameter);

            // Act
            var result = method.AnalyzeSemantics();

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public void ValidateNode_InvalidChildNodeType_ReturnsError()
        {
            // Arrange
            var classNode = new UnifiedASTNode(_validator)
            {
                NodeType = "Class",
                Name = "TestClass",
                SourceLanguage = "C#"
            };

            var invalidChild = new UnifiedASTNode(_validator)
            {
                NodeType = "InvalidType",
                Name = "Invalid"
            };

            classNode.AddChild(invalidChild);

            // Act
            var errors = classNode.Validate().ToList();

            // Assert
            Assert.IsTrue(errors.Any(e => e.ErrorCode == "AST004"), 
                "Should detect invalid child node type");
        }

        [TestMethod]
        public void GetAncestors_MultipleAncestors_ReturnsCorrectHierarchy()
        {
            // Arrange
            var root = new UnifiedASTNode(_validator)
            {
                NodeType = "Namespace",
                Name = "TestNamespace"
            };

            var classNode = new UnifiedASTNode(_validator)
            {
                NodeType = "Class",
                Name = "TestClass"
            };

            var methodNode = new UnifiedASTNode(_validator)
            {
                NodeType = "Method",
                Name = "TestMethod"
            };

            root.AddChild(classNode);
            classNode.AddChild(methodNode);

            // Act
            var ancestors = methodNode.GetAncestors().ToList();

            // Assert
            Assert.AreEqual(2, ancestors.Count);
            Assert.AreEqual("Class", ancestors[0].NodeType);
            Assert.AreEqual("Namespace", ancestors[1].NodeType);
        }
    }
}