using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBuddy.E2ETests.Scenarios
{
    public class CrossLanguageValidationE2ETests : E2ETestBase
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            services.AddSingleton<IASTConverter, CSharpASTConverter>();
            services.AddSingleton<IASTConverter, PythonASTConverter>();
            services.AddSingleton<IASTConverter, JavaScriptASTConverter>();
            services.AddSingleton<IASTValidator, ASTSemanticValidator>();
            services.AddSingleton<UnifiedTypeSystem>();
        }

        [Fact]
        public async Task CrossLanguageValidation_TypeCompatibility_ShouldValidate()
        {
            // Arrange
            var typeSystem = ServiceProvider.GetRequiredService<UnifiedTypeSystem>();
            var csharpConverter = ServiceProvider.GetRequiredService<CSharpASTConverter>();
            var pythonConverter = ServiceProvider.GetRequiredService<PythonASTConverter>();

            var csharpCode = @"
                public class DataModel {
                    public int Id { get; set; }
                    public string Name { get; set; }
                }";

            var pythonCode = @"
                class DataModel:
                    def __init__(self):
                        self.id = 0
                        self.name = ''";

            // Act
            var csharpAst = await csharpConverter.ConvertToASTAsync(csharpCode);
            var pythonAst = await pythonConverter.ConvertToASTAsync(pythonCode);

            var typeValidation = await typeSystem.ValidateTypeCompatibilityAsync(csharpAst, pythonAst);

            // Assert
            typeValidation.IsCompatible.Should().BeTrue();
            typeValidation.TypeMappings.Should().ContainKey("DataModel");
            typeValidation.TypeMappings["DataModel"].Properties.Should().HaveCount(2);
        }

        [Fact]
        public async Task CrossLanguageValidation_PatternMatching_ShouldDetectSimilarPatterns()
        {
            // Arrange
            var astValidator = ServiceProvider.GetRequiredService<ASTSemanticValidator>();
            var csharpConverter = ServiceProvider.GetRequiredService<CSharpASTConverter>();
            var jsConverter = ServiceProvider.GetRequiredService<JavaScriptASTConverter>();

            var csharpCode = @"
                public class ResourceManager {
                    private readonly object _lock = new object();
                    public void AcquireResource() {
                        lock(_lock) {
                            // acquire resource
                        }
                    }
                }";

            var jsCode = @"
                class ResourceManager {
                    #lockObj = new Object();
                    async acquireResource() {
                        await this.#lockObj.acquire();
                        try {
                            // acquire resource
                        } finally {
                            this.#lockObj.release();
                        }
                    }
                }";

            // Act
            var csharpAst = await csharpConverter.ConvertToASTAsync(csharpCode);
            var jsAst = await jsConverter.ConvertToASTAsync(jsCode);

            var patternAnalysis = await astValidator.AnalyzePatternSimilarityAsync(csharpAst, jsAst);

            // Assert
            patternAnalysis.HasSimilarPatterns.Should().BeTrue();
            patternAnalysis.PatternMatches.Should().Contain(m => 
                m.PatternType == "SynchronizationMechanism" && 
                m.Confidence > 0.8);
        }

        [Fact]
        public async Task CrossLanguageValidation_SemanticAnalysis_ShouldValidateConsistency()
        {
            // Arrange
            var astValidator = ServiceProvider.GetRequiredService<ASTSemanticValidator>();
            var csharpConverter = ServiceProvider.GetRequiredService<CSharpASTConverter>();
            var pythonConverter = ServiceProvider.GetRequiredService<PythonASTConverter>();

            var csharpCode = @"
                public interface IDataProcessor {
                    void ProcessData(byte[] data);
                    bool ValidateData(byte[] data);
                }
                
                public class DataProcessor : IDataProcessor {
                    public void ProcessData(byte[] data) {
                        if (ValidateData(data)) {
                            // process data
                        }
                    }
                    
                    public bool ValidateData(byte[] data) {
                        return data != null && data.Length > 0;
                    }
                }";

            var pythonCode = @"
                class IDataProcessor:
                    def process_data(self, data):
                        pass
                    def validate_data(self, data):
                        pass

                class DataProcessor(IDataProcessor):
                    def process_data(self, data):
                        if self.validate_data(data):
                            # process data
                            pass
                    
                    def validate_data(self, data):
                        return data is not None and len(data) > 0";

            // Act
            var csharpAst = await csharpConverter.ConvertToASTAsync(csharpCode);
            var pythonAst = await pythonConverter.ConvertToASTAsync(pythonCode);

            var semanticAnalysis = await astValidator.ValidateSemanticConsistencyAsync(csharpAst, pythonAst);

            // Assert
            semanticAnalysis.IsConsistent.Should().BeTrue();
            semanticAnalysis.InterfaceImplementations.Should().ContainKey("IDataProcessor");
            semanticAnalysis.MethodSemantics.Should().Contain(m => 
                m.Name == "ProcessData" && 
                m.SemanticEquivalenceScore > 0.9);
        }
    }
}