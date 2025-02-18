using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Implementation.CodeValidation.Memory;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.AST;
using Xunit;
using FluentAssertions;

namespace CodeBuddy.IntegrationTests.CrossLanguageValidation
{
    public class ValidationPipelineTests : IDisposable
    {
        private readonly ValidationPipeline _pipeline;
        private readonly ResourceCleanupService _cleanupService;
        private readonly MemoryLeakDetector _memoryLeakDetector;
        private readonly ASTSemanticValidator _semanticValidator;
        private readonly ResourceReleaseMonitor _resourceMonitor;

        public ValidationPipelineTests()
        {
            _cleanupService = new ResourceCleanupService();
            _memoryLeakDetector = new MemoryLeakDetector();
            _semanticValidator = new ASTSemanticValidator();
            _resourceMonitor = new ResourceReleaseMonitor();
            _pipeline = new ValidationPipeline();
        }

        [Fact]
        public async Task CrossLanguageValidation_ShouldValidateInterconnectedComponents()
        {
            // Arrange
            var csharpCode = @"
                public class DataProcessor {
                    private PythonAnalyzer _analyzer;
                    public void ProcessData(string data) {
                        _analyzer.Process(data);
                    }
                }";

            var pythonCode = @"
                class PythonAnalyzer:
                    def process(self, data):
                        return json.loads(data)";

            // Act
            var result = await _pipeline.ValidateAsync(new[] { 
                new CodeSnippet { Content = csharpCode, Language = "C#" },
                new CodeSnippet { Content = pythonCode, Language = "Python" }
            });

            // Assert
            result.Success.Should().BeTrue();
            result.CrossLanguageValidation.Should().NotBeNull();
            result.ResourceMetrics.Should().NotBeNull();
        }

        [Fact]
        public async Task ResourceManagement_ShouldTrackCrossLanguageResourceUsage()
        {
            // Arrange
            var jsCode = @"
                async function processData(data) {
                    const result = await pythonProcessor.analyze(data);
                    return csharpFormatter.format(result);
                }";

            // Act
            var metrics = await _resourceMonitor.TrackResourceUsageAsync(() => 
                _pipeline.ValidateAsync(new[] { new CodeSnippet { Content = jsCode, Language = "JavaScript" } }));

            // Assert
            metrics.TotalMemoryUsed.Should().BeLessThan(1000000); // 1MB
            metrics.ResourceLeaks.Should().BeEmpty();
        }

        [Fact]
        public async Task MemoryLeakDetection_ShouldIdentifyLeaksAcrossLanguages()
        {
            // Arrange
            var codeSnippets = new[] {
                new CodeSnippet { Content = "class ResourceHog { }", Language = "C#" },
                new CodeSnippet { Content = "def process(): pass", Language = "Python" },
                new CodeSnippet { Content = "function analyze() { }", Language = "JavaScript" }
            };

            // Act
            var leakReport = await _memoryLeakDetector.AnalyzeAsync(() =>
                _pipeline.ValidateAsync(codeSnippets));

            // Assert
            leakReport.HasLeaks.Should().BeFalse();
            leakReport.LeakSources.Should().BeEmpty();
        }

        [Fact]
        public async Task SemanticValidation_ShouldValidateASTAcrossLanguages()
        {
            // Arrange
            var csharpClass = @"
                public class DataService {
                    public async Task<string> GetDataAsync() => await jsClient.FetchDataAsync();
                }";

            var jsFunction = @"
                async function fetchData() {
                    const data = await pythonProcessor.getData();
                    return JSON.stringify(data);
                }";

            // Act
            var astValidation = await _semanticValidator.ValidateAsync(new[] {
                new UnifiedASTNode { Code = csharpClass, Language = "C#" },
                new UnifiedASTNode { Code = jsFunction, Language = "JavaScript" }
            });

            // Assert
            astValidation.IsValid.Should().BeTrue();
            astValidation.CrossLanguageReferences.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidationPipeline_ShouldHandleCachingAcrossLanguages()
        {
            // Arrange
            var snippets = new[] {
                new CodeSnippet { Content = "public class TestClass {}", Language = "C#" },
                new CodeSnippet { Content = "def test_function(): pass", Language = "Python" }
            };

            // Act
            var firstRun = await _pipeline.ValidateAsync(snippets);
            var secondRun = await _pipeline.ValidateAsync(snippets);

            // Assert
            secondRun.CacheHit.Should().BeTrue();
            secondRun.ValidationTime.Should().BeLessThan(firstRun.ValidationTime);
        }

        public void Dispose()
        {
            _cleanupService.CleanupResources();
            _memoryLeakDetector.Dispose();
            _resourceMonitor.Dispose();
        }
    }
}