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
    public class PatternMatchingEngineTests
    {
        private readonly Mock<IPatternRepository> _mockRepository;
        private readonly PatternMatchingEngine _engine;

        public PatternMatchingEngineTests()
        {
            _mockRepository = new Mock<IPatternRepository>();
            _engine = new PatternMatchingEngine(_mockRepository.Object);
        }

        [Fact]
        public async Task DetectPatternsAsync_EmptyAST_ReturnsEmptyResults()
        {
            // Arrange
            var emptyAst = new UnifiedASTNode();
            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern>());

            // Act
            var results = await _engine.DetectPatternsAsync(emptyAst, "test.cs");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task DetectPatternsAsync_WithStructuralPattern_MatchesCorrectly()
        {
            // Arrange
            var ast = CreateTestASTWithStructuralPattern();
            var pattern = new CodePattern
            {
                Id = "STRUCT_001",
                Type = PatternType.Structural,
                Name = "Test Structural Pattern",
                PatternExpression = "class[name='TestClass'] > method[name='TestMethod']",
                LanguageScope = "C#"
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "test.cs");

            // Assert
            Assert.Single(results);
            Assert.Equal(pattern.Id, results[0].PatternId);
            Assert.True(results[0].ConfidenceScore > 0.8);
        }

        [Fact]
        public async Task DetectPatternsAsync_CrossLanguagePattern_MatchesAcrossLanguages()
        {
            // Arrange
            var csharpAst = CreateTestCSharpAST();
            var pattern = new CodePattern
            {
                Id = "CROSS_001",
                Type = PatternType.Structural,
                Name = "Cross-Language Method Pattern",
                PatternExpression = "method[visibility='public']",
                LanguageScope = null // null indicates cross-language pattern
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var csharpResults = await _engine.DetectPatternsAsync(csharpAst, "test.cs");
            var pythonResults = await _engine.DetectPatternsAsync(CreateTestPythonAST(), "test.py");
            var jsResults = await _engine.DetectPatternsAsync(CreateTestJavaScriptAST(), "test.js");

            // Assert
            Assert.NotEmpty(csharpResults);
            Assert.NotEmpty(pythonResults);
            Assert.NotEmpty(jsResults);
        }

        [Fact]
        public async Task DetectPatternsAsync_ComplexNestedPattern_MatchesCorrectly()
        {
            // Arrange
            var ast = CreateComplexNestedAST();
            var pattern = new CodePattern
            {
                Id = "NESTED_001",
                Type = PatternType.Structural,
                Name = "Nested Structure Pattern",
                PatternExpression = "class > method > if > for",
                LanguageScope = "C#"
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "test.cs");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(pattern.Id, results[0].PatternId);
            Assert.NotNull(results[0].Location);
        }

        [Fact]
        public async Task DetectPatternsAsync_FuzzyMatching_ReturnsPartialMatches()
        {
            // Arrange
            var ast = CreateTestASTWithPartialMatch();
            var pattern = new CodePattern
            {
                Id = "FUZZY_001",
                Type = PatternType.Structural,
                Name = "Fuzzy Match Pattern",
                PatternExpression = "class[name='TestClass'] > method[name~='Test']",
                IsFuzzyMatching = true,
                MinConfidenceThreshold = 0.7
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "test.cs");

            // Assert
            Assert.NotEmpty(results);
            Assert.True(results[0].IsFuzzyMatch);
            Assert.True(results[0].ConfidenceScore >= pattern.MinConfidenceThreshold);
        }

        [Fact]
        public async Task DetectPatternsAsync_SemanticPattern_MatchesSemanticRules()
        {
            // Arrange
            var ast = CreateTestASTWithSemanticContext();
            var pattern = new CodePattern
            {
                Id = "SEM_001",
                Type = PatternType.Semantic,
                Name = "Semantic Analysis Pattern",
                PatternExpression = "method[writes='field'] && !method[synchronized='true']",
                Severity = PatternSeverity.Warning
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, "test.cs");

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MatchDetails.ContainsKey("unsynchronizedAccess"));
        }

        [Fact]
        public async Task DetectPatternsAsync_InvalidPattern_HandlesErrorGracefully()
        {
            // Arrange
            var ast = new UnifiedASTNode();
            var pattern = new CodePattern
            {
                Id = "INVALID_001",
                Type = PatternType.Structural,
                Name = "Invalid Pattern",
                PatternExpression = "[[invalid]] syntax"
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _engine.DetectPatternsAsync(ast, "test.cs")
            );
        }

        [Fact]
        public async Task DetectPatternsAsync_PerformanceTest_HandlesLargeAST()
        {
            // Arrange
            var largeAst = CreateLargeAST();
            var patterns = CreateTestPatterns(100); // Test with 100 patterns

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(patterns);

            // Act
            var startTime = DateTime.Now;
            var results = await _engine.DetectPatternsAsync(largeAst, "large.cs");
            var duration = DateTime.Now - startTime;

            // Assert
            Assert.True(duration.TotalSeconds < 5); // Should complete within 5 seconds
            Assert.NotEmpty(results);
        }

        [Theory]
        [InlineData("C#", "method[returns='void']")]
        [InlineData("Python", "function[args>0]")]
        [InlineData("JavaScript", "function[async='true']")]
        public async Task DetectPatternsAsync_LanguageSpecificPatterns_MatchCorrectly(string language, string expression)
        {
            // Arrange
            var ast = CreateLanguageSpecificAST(language);
            var pattern = new CodePattern
            {
                Id = $"{language}_001",
                Type = PatternType.Structural,
                Name = $"{language} Specific Pattern",
                PatternExpression = expression,
                LanguageScope = language
            };

            _mockRepository.Setup(r => r.GetPatternsAsync())
                         .ReturnsAsync(new List<CodePattern> { pattern });

            // Act
            var results = await _engine.DetectPatternsAsync(ast, $"test.{GetFileExtension(language)}");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal(pattern.Id, results[0].PatternId);
        }

        #region Helper Methods

        private UnifiedASTNode CreateTestASTWithStructuralPattern()
        {
            return new UnifiedASTNode
            {
                Type = "class",
                Properties = new Dictionary<string, string> { { "name", "TestClass" } },
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "method",
                        Properties = new Dictionary<string, string> { { "name", "TestMethod" } }
                    }
                }
            };
        }

        private UnifiedASTNode CreateTestCSharpAST()
        {
            return new UnifiedASTNode
            {
                Type = "compilation_unit",
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "method",
                        Properties = new Dictionary<string, string>
                        {
                            { "visibility", "public" },
                            { "name", "TestMethod" }
                        }
                    }
                }
            };
        }

        private UnifiedASTNode CreateTestPythonAST()
        {
            return new UnifiedASTNode
            {
                Type = "module",
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "function_def",
                        Properties = new Dictionary<string, string>
                        {
                            { "visibility", "public" },
                            { "name", "test_function" }
                        }
                    }
                }
            };
        }

        private UnifiedASTNode CreateTestJavaScriptAST()
        {
            return new UnifiedASTNode
            {
                Type = "program",
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "function_declaration",
                        Properties = new Dictionary<string, string>
                        {
                            { "visibility", "public" },
                            { "name", "testFunction" }
                        }
                    }
                }
            };
        }

        private UnifiedASTNode CreateComplexNestedAST()
        {
            return new UnifiedASTNode
            {
                Type = "class",
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "method",
                        Children = new List<UnifiedASTNode>
                        {
                            new UnifiedASTNode
                            {
                                Type = "if",
                                Children = new List<UnifiedASTNode>
                                {
                                    new UnifiedASTNode
                                    {
                                        Type = "for"
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private UnifiedASTNode CreateTestASTWithPartialMatch()
        {
            return new UnifiedASTNode
            {
                Type = "class",
                Properties = new Dictionary<string, string> { { "name", "TestClass" } },
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "method",
                        Properties = new Dictionary<string, string> { { "name", "TestVariant" } }
                    }
                }
            };
        }

        private UnifiedASTNode CreateTestASTWithSemanticContext()
        {
            return new UnifiedASTNode
            {
                Type = "method",
                Properties = new Dictionary<string, string>
                {
                    { "name", "UpdateField" },
                    { "synchronized", "false" }
                },
                Children = new List<UnifiedASTNode>
                {
                    new UnifiedASTNode
                    {
                        Type = "assignment",
                        Properties = new Dictionary<string, string>
                        {
                            { "target", "field" },
                            { "value", "newValue" }
                        }
                    }
                }
            };
        }

        private UnifiedASTNode CreateLargeAST()
        {
            var root = new UnifiedASTNode { Type = "compilation_unit" };
            var children = new List<UnifiedASTNode>();

            for (int i = 0; i < 1000; i++) // Create 1000 nodes
            {
                children.Add(new UnifiedASTNode
                {
                    Type = "class",
                    Properties = new Dictionary<string, string> { { "name", $"Class{i}" } },
                    Children = new List<UnifiedASTNode>
                    {
                        new UnifiedASTNode
                        {
                            Type = "method",
                            Properties = new Dictionary<string, string> { { "name", $"Method{i}" } }
                        }
                    }
                });
            }

            root.Children = children;
            return root;
        }

        private UnifiedASTNode CreateLanguageSpecificAST(string language)
        {
            switch (language)
            {
                case "C#":
                    return CreateTestCSharpAST();
                case "Python":
                    return CreateTestPythonAST();
                case "JavaScript":
                    return CreateTestJavaScriptAST();
                default:
                    throw new ArgumentException($"Unsupported language: {language}");
            }
        }

        private List<CodePattern> CreateTestPatterns(int count)
        {
            var patterns = new List<CodePattern>();
            for (int i = 0; i < count; i++)
            {
                patterns.Add(new CodePattern
                {
                    Id = $"TEST_{i:D3}",
                    Type = PatternType.Structural,
                    Name = $"Test Pattern {i}",
                    PatternExpression = $"class[name='Class{i}']"
                });
            }
            return patterns;
        }

        private string GetFileExtension(string language)
        {
            switch (language)
            {
                case "C#": return "cs";
                case "Python": return "py";
                case "JavaScript": return "js";
                default: throw new ArgumentException($"Unsupported language: {language}");
            }
        }

        #endregion
    }
}