using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.CodeValidation.AST;
using CodeBuddy.Core.Models.AST;
using Xunit;
using FluentAssertions;

namespace CodeBuddy.IntegrationTests.SemanticValidation
{
    public class ASTSemanticTests
    {
        private readonly ASTSemanticValidator _validator;
        private readonly CSharpASTConverter _csharpConverter;
        private readonly JavaScriptASTConverter _jsConverter;
        private readonly PythonASTConverter _pythonConverter;

        public ASTSemanticTests()
        {
            _validator = new ASTSemanticValidator();
            _csharpConverter = new CSharpASTConverter();
            _jsConverter = new JavaScriptASTConverter();
            _pythonConverter = new PythonASTConverter();
        }

        [Fact]
        public async Task SemanticValidation_ShouldValidateAcrossLanguages()
        {
            // Arrange
            var csharpCode = @"
                public interface IDataProcessor {
                    Task<Dictionary<string, object>> ProcessAsync(string data);
                }";

            var pythonCode = @"
                class DataProcessor:
                    async def process_async(self, data: str) -> dict:
                        return {'result': await self.parse(data)}";

            var jsCode = @"
                class DataProcessor {
                    async processAsync(data) {
                        return { result: await this.parse(data) };
                    }
                }";

            // Act
            var csharpAst = await _csharpConverter.ConvertToUnifiedASTAsync(csharpCode);
            var pythonAst = await _pythonConverter.ConvertToUnifiedASTAsync(pythonCode);
            var jsAst = await _jsConverter.ConvertToUnifiedASTAsync(jsCode);

            var validationResult = await _validator.ValidateCompatibilityAsync(
                new[] { csharpAst, pythonAst, jsAst });

            // Assert
            validationResult.IsValid.Should().BeTrue();
            validationResult.MethodSignatureMatches.Should().NotBeEmpty();
            validationResult.TypeCompatibility.Should().BeTrue();
        }

        [Fact]
        public async Task TypeCompatibility_ShouldValidateAcrossLanguages()
        {
            // Arrange
            var csharpType = @"
                public class DataModel {
                    public int Id { get; set; }
                    public string Name { get; set; }
                    public List<string> Tags { get; set; }
                }";

            var pythonType = @"
                class DataModel:
                    def __init__(self):
                        self.id: int = 0
                        self.name: str = ''''
                        self.tags: List[str] = []";

            var jsType = @"
                class DataModel {
                    constructor() {
                        this.id = 0;
                        this.name = '';
                        this.tags = [];
                    }
                }";

            // Act
            var compatibilityResult = await _validator.ValidateTypeCompatibilityAsync(
                new Dictionary<string, string> {
                    { "C#", csharpType },
                    { "Python", pythonType },
                    { "JavaScript", jsType }
                });

            // Assert
            compatibilityResult.IsCompatible.Should().BeTrue();
            compatibilityResult.PropertyMappings.Should().HaveCount(3);
            compatibilityResult.TypeConversions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task MethodSignatures_ShouldValidateAcrossLanguages()
        {
            // Arrange
            var csharpMethod = @"
                public async Task<IEnumerable<string>> ProcessDataAsync(
                    string input, 
                    IDictionary<string, object> options = null)";

            var pythonMethod = @"
                async def process_data_async(
                    input: str, 
                    options: Optional[Dict[str, Any]] = None) -> List[str]:";

            var jsMethod = @"
                async processDataAsync(input, options = null) {
                    return Promise<string[]>;
                }";

            // Act
            var signatureAnalysis = await _validator.ValidateMethodSignaturesAsync(
                new Dictionary<string, string> {
                    { "C#", csharpMethod },
                    { "Python", pythonMethod },
                    { "JavaScript", jsMethod }
                });

            // Assert
            signatureAnalysis.IsCompatible.Should().BeTrue();
            signatureAnalysis.ParameterMappings.Should().HaveCount(2);
            signatureAnalysis.ReturnTypeCompatibility.Should().BeTrue();
        }

        [Fact]
        public async Task AsyncPatterns_ShouldValidateAcrossLanguages()
        {
            // Arrange
            var csharpAsync = @"
                public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation) {
                    return await operation();
                }";

            var pythonAsync = @"
                async def execute_async(self, operation: Callable[[], Awaitable[T]]) -> T:
                    return await operation()";

            var jsAsync = @"
                async executeAsync(operation) {
                    return await operation();
                }";

            // Act
            var asyncAnalysis = await _validator.ValidateAsyncPatternsAsync(
                new Dictionary<string, string> {
                    { "C#", csharpAsync },
                    { "Python", pythonAsync },
                    { "JavaScript", jsAsync }
                });

            // Assert
            asyncAnalysis.IsValid.Should().BeTrue();
            asyncAnalysis.AsyncPatternCompatibility.Should().BeTrue();
            asyncAnalysis.AwaitableTypes.Should().HaveCount(3);
        }
    }
}