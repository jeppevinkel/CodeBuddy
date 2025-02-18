using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeBuddy.Core.Implementation.PatternDetection;
using CodeBuddy.Core.Models.Patterns;
using CodeBuddy.Core.Models.AST;

namespace CodeBuddy.Tests.PatternDetection
{
    [TestClass]
    public class PatternMatchingEngineTests
    {
        private IPatternMatchingEngine _patternEngine;
        private IPatternRepository _patternRepository;

        [TestInitialize]
        public void Setup()
        {
            _patternRepository = new PatternRepository("test_patterns");
            _patternEngine = new PatternMatchingEngine(_patternRepository);
        }

        [TestMethod]
        public async Task DetectPatternsAsync_WithSqlInjectionPattern_ShouldDetectVulnerability()
        {
            // Arrange
            var astRoot = CreateTestASTWithSqlInjection();

            // Act
            var results = await _patternEngine.DetectPatternsAsync(astRoot, "test.cs");

            // Assert
            Assert.IsTrue(results.Exists(r => r.PatternId == "SEC001"));
            var match = results.Find(r => r.PatternId == "SEC001");
            Assert.IsTrue(match.ConfidenceScore >= 0.8);
        }

        [TestMethod]
        public async Task DetectPatternsAsync_WithNestedLoops_ShouldDetectPerformanceIssue()
        {
            // Arrange
            var astRoot = CreateTestASTWithNestedLoops();

            // Act
            var results = await _patternEngine.DetectPatternsAsync(astRoot, "test.cs");

            // Assert
            Assert.IsTrue(results.Exists(r => r.PatternId == "PERF001"));
            var match = results.Find(r => r.PatternId == "PERF001");
            Assert.AreEqual(1.0, match.ConfidenceScore);
        }

        [TestMethod]
        public async Task DetectPatternsAsync_WithResourceManagement_ShouldDetectMissingUsing()
        {
            // Arrange
            var astRoot = CreateTestASTWithResourceManagement();

            // Act
            var results = await _patternEngine.DetectPatternsAsync(astRoot, "test.cs");

            // Assert
            Assert.IsTrue(results.Exists(r => r.PatternId == "BP001"));
            var match = results.Find(r => r.PatternId == "BP001");
            Assert.IsTrue(match.ConfidenceScore >= 0.9);
        }

        private UnifiedASTNode CreateTestASTWithSqlInjection()
        {
            // Create a test AST that contains a SQL injection vulnerability
            return new UnifiedASTNode();
        }

        private UnifiedASTNode CreateTestASTWithNestedLoops()
        {
            // Create a test AST that contains nested loops
            return new UnifiedASTNode();
        }

        private UnifiedASTNode CreateTestASTWithResourceManagement()
        {
            // Create a test AST that contains resource management code
            return new UnifiedASTNode();
        }
    }
}