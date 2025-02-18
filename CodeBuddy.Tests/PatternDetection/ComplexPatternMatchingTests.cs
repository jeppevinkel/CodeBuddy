using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.PatternDetection;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Models.Patterns;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.PatternDetection
{
    public class ComplexPatternMatchingTests
    {
        private readonly Mock<IPatternRepository> _mockRepository;
        private readonly PatternMatchingEngine _engine;

        public ComplexPatternMatchingTests()
        {
            _mockRepository = new Mock<IPatternRepository>();
            _engine = new PatternMatchingEngine(_mockRepository.Object);
        }

        [Fact]
        public async Task DetectPatternsAsync_NestedPatternWithConditionals_MatchesCorrectly()
        {
            // Arrange
            var ast = TestASTBuilder.CreateClassNode("TestClass",
                new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateMethodNode("Process",
                        children: new List<UnifiedASTNode>
                        {
                            TestASTBuilder.CreateIfNode(
                                TestASTBuilder.CreateExpressionNode("condition"),
                                new List<UnifiedASTNode>
                                {
                                    TestASTBuilder.CreateLoopNode("for",
                                        TestASTBuilder.CreateExpressionNode("loop_condition"),
                                        new List<UnifiedASTNode>
                                        {
                                            TestASTBuilder.CreateMethodNode("NestedCall")
                                        })
                                })
                        })
                });

            var pattern = new CodePattern
            {
                Id = "COMPLEX_001",
                Type = PatternType.Structural,
                Name = "Complex Nested Pattern",
                PatternExpression = "class[name='TestClass'] > method > if > for > method"
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "test.cs");

            // Assert
            Assert.Single(results);
            Assert.Equal(pattern.Id, results[0].PatternId);
        }

        [Fact]
        public async Task DetectPatternsAsync_CrossLanguageInheritance_MatchesCorrectly()
        {
            // Arrange
            var csharpAst = TestASTBuilder.CreateClassNode("BaseClass",
                new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateMethodNode("VirtualMethod", visibility: "public")
                });

            var pythonAst = TestASTBuilder.CreateClassNode("DerivedClass",
                new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateMethodNode("VirtualMethod", visibility: "public")
                });

            var pattern = new CodePattern
            {
                Id = "INHERIT_001",
                Type = PatternType.Semantic,
                Name = "Cross-Language Virtual Method Pattern",
                PatternExpression = "class > method[visibility='public'][virtual='true']",
                LanguageScope = null // Cross-language pattern
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var csharpResults = await _engine.DetectPatternsAsync(csharpAst, "base.cs");
            var pythonResults = await _engine.DetectPatternsAsync(pythonAst, "derived.py");

            // Assert
            Assert.NotEmpty(csharpResults);
            Assert.NotEmpty(pythonResults);
        }

        [Fact]
        public async Task DetectPatternsAsync_SecurityPatternAcrossLanguages_MatchesVulnerabilities()
        {
            // Arrange
            var patterns = new List<CodePattern>
            {
                new CodePattern
                {
                    Id = "SEC_SQL_001",
                    Type = PatternType.Security,
                    Name = "SQL Injection Pattern",
                    PatternExpression = "method[contains='sql'] && !method[contains='parameterized']",
                    Severity = PatternSeverity.Critical
                },
                new CodePattern
                {
                    Id = "SEC_XSS_001",
                    Type = PatternType.Security,
                    Name = "Cross-Site Scripting Pattern",
                    PatternExpression = "method[writes='html'] && !method[contains='encode']",
                    Severity = PatternSeverity.Critical
                }
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(patterns);

            var csharpAst = CreateVulnerableMethodAST("ExecuteSql", "sql");
            var pythonAst = CreateVulnerableMethodAST("render_html", "html");
            var jsAst = CreateVulnerableMethodAST("updateContent", "html");

            // Act
            var csharpResults = await _engine.DetectPatternsAsync(csharpAst, "data.cs");
            var pythonResults = await _engine.DetectPatternsAsync(pythonAst, "template.py");
            var jsResults = await _engine.DetectPatternsAsync(jsAst, "render.js");

            // Assert
            Assert.Contains(csharpResults, r => r.PatternId == "SEC_SQL_001");
            Assert.Contains(pythonResults, r => r.PatternId == "SEC_XSS_001");
            Assert.Contains(jsResults, r => r.PatternId == "SEC_XSS_001");
        }

        [Fact]
        public async Task DetectPatternsAsync_PerformanceAntiPatterns_IdentifiesIssues()
        {
            // Arrange
            var pattern = new CodePattern
            {
                Id = "PERF_001",
                Type = PatternType.Performance,
                Name = "Nested Loop Anti-Pattern",
                PatternExpression = "method > for > for",
                Severity = PatternSeverity.Warning
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            var ast = TestASTBuilder.CreateMethodNode("ProcessData",
                children: new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateLoopNode("for",
                        TestASTBuilder.CreateExpressionNode("outer_condition"),
                        new List<UnifiedASTNode>
                        {
                            TestASTBuilder.CreateLoopNode("for",
                                TestASTBuilder.CreateExpressionNode("inner_condition"),
                                new List<UnifiedASTNode>())
                        })
                });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "process.cs");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(PatternType.Performance, results[0].PatternId);
        }

        [Fact]
        public async Task DetectPatternsAsync_ResourceManagementPattern_DetectsLeaks()
        {
            // Arrange
            var pattern = new CodePattern
            {
                Id = "RES_001",
                Type = PatternType.BestPractice,
                Name = "Resource Cleanup Pattern",
                PatternExpression = "method[creates='resource'] && !method[contains='dispose']",
                Severity = PatternSeverity.Warning
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            var ast = TestASTBuilder.CreateMethodNode("OpenConnection",
                children: new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateVariableNode("connection", "SqlConnection", "new SqlConnection()"),
                    TestASTBuilder.CreateMethodNode("ExecuteCommand")
                    // Missing dispose call
                });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "database.cs");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(pattern.Id, results[0].PatternId);
        }

        private UnifiedASTNode CreateVulnerableMethodAST(string methodName, string vulnerability)
        {
            return TestASTBuilder.CreateMethodNode(methodName,
                children: new List<UnifiedASTNode>
                {
                    TestASTBuilder.CreateVariableNode("query", "string",
                        $"user_input + {vulnerability}_command"),
                    TestASTBuilder.CreateMethodNode("Execute",
                        children: new List<UnifiedASTNode>
                        {
                            TestASTBuilder.CreateVariableNode("result", "object")
                        })
                });
        }
    }
}