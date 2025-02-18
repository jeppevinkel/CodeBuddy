using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.PatternDetection;
using CodeBuddy.Core.Models.Patterns;
using CodeBuddy.Core.Models;
using Xunit;
using FluentAssertions;

namespace CodeBuddy.IntegrationTests.PatternMatching
{
    public class CrossLanguagePatternTests
    {
        private readonly PatternMatchingEngine _patternEngine;
        private readonly PatternRepository _patternRepo;

        public CrossLanguagePatternTests()
        {
            _patternEngine = new PatternMatchingEngine();
            _patternRepo = new PatternRepository();
        }

        [Fact]
        public async Task PatternMatching_ShouldDetectCrossLanguagePatterns()
        {
            // Arrange
            var csharpCode = @"
                public class ApiClient {
                    private readonly IJavaScriptInterop _js;
                    public async Task<dynamic> GetDataAsync() {
                        return await _js.InvokeAsync<dynamic>(""fetchData"");
                    }
                }";

            var jsCode = @"
                async function fetchData() {
                    const result = await pythonProcessor.processData();
                    return JSON.parse(result);
                }";

            var pythonCode = @"
                def process_data():
                    return json.dumps({'status': 'success'})";

            var pattern = new CodePattern
            {
                Name = "Cross-Language API Call Chain",
                Description = "Detects chained API calls across different languages",
                Severity = PatternSeverity.Information
            };

            // Act
            var matches = await _patternEngine.MatchPatternAsync(pattern, new[] {
                new CodeSnippet { Content = csharpCode, Language = "C#" },
                new CodeSnippet { Content = jsCode, Language = "JavaScript" },
                new CodeSnippet { Content = pythonCode, Language = "Python" }
            });

            // Assert
            matches.Should().NotBeEmpty();
            matches.Should().HaveCountGreaterThan(0);
            matches[0].CrossLanguageReferences.Should().HaveCount(2);
        }

        [Fact]
        public async Task PatternRepository_ShouldManageCrossLanguagePatterns()
        {
            // Arrange
            var patterns = new List<CodePattern>
            {
                new CodePattern {
                    Name = "Resource Leak Pattern",
                    Description = "Detects potential resource leaks across language boundaries",
                    Severity = PatternSeverity.Warning
                },
                new CodePattern {
                    Name = "Memory Management Pattern",
                    Description = "Validates memory management across different language runtimes",
                    Severity = PatternSeverity.Critical
                }
            };

            // Act
            await _patternRepo.AddPatternsAsync(patterns);
            var storedPatterns = await _patternRepo.GetPatternsAsync();

            // Assert
            storedPatterns.Should().HaveCount(2);
            storedPatterns.Should().Contain(p => p.Name == "Resource Leak Pattern");
            storedPatterns.Should().Contain(p => p.Name == "Memory Management Pattern");
        }

        [Fact]
        public async Task PatternValidation_ShouldCheckCrossLanguageSemantics()
        {
            // Arrange
            var csharpCode = @"
                public interface IPythonInterop {
                    Task<string> ExecuteAsync(string script);
                }";

            var pythonCode = @"
                class PythonExecutor:
                    def execute_async(self, script):
                        return await async_execute(script)";

            // Act
            var validationResult = await _patternEngine.ValidateInterfaceCompatibilityAsync(
                new CodeSnippet { Content = csharpCode, Language = "C#" },
                new CodeSnippet { Content = pythonCode, Language = "Python" }
            );

            // Assert
            validationResult.IsValid.Should().BeTrue();
            validationResult.MethodMappings.Should().ContainKey("ExecuteAsync");
            validationResult.MethodMappings["ExecuteAsync"].Should().Be("execute_async");
        }

        [Fact]
        public async Task ComplexPatternMatching_ShouldHandleNestedPatterns()
        {
            // Arrange
            var jsCode = @"
                class ApiWrapper {
                    async callCSharpMethod() {
                        const result = await csharpInterop.ProcessData();
                        return await this.processPythonResult(result);
                    }
                    
                    async processPythonResult(data) {
                        return await pythonProcessor.format(data);
                    }
                }";

            // Act
            var analysis = await _patternEngine.AnalyzeNestedPatternsAsync(
                new CodeSnippet { Content = jsCode, Language = "JavaScript" });

            // Assert
            analysis.PatternDepth.Should().BeGreaterThan(1);
            analysis.CrossLanguageCalls.Should().HaveCount(2);
            analysis.CallChain.Should().ContainInOrder(
                new[] { "JavaScript", "C#", "JavaScript", "Python" });
        }
    }
}