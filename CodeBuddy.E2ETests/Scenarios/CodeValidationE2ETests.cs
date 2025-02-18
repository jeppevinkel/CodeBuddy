using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using CodeBuddy.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBuddy.E2ETests.Scenarios
{
    public class CodeValidationE2ETests : E2ETestBase
    {
        private ICodeValidator _csharpValidator;
        private ICodeValidator _pythonValidator;
        private ICodeValidator _javascriptValidator;

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            services.AddSingleton<ICodeValidator, CSharpCodeValidator>();
            services.AddSingleton<ICodeValidator, PythonCodeValidator>();
            services.AddSingleton<ICodeValidator, JavaScriptCodeValidator>();
            services.AddSingleton<ValidationPipeline>();
        }

        protected override async Task OnTestInitializeAsync()
        {
            await base.OnTestInitializeAsync();

            _csharpValidator = ServiceProvider.GetRequiredService<CSharpCodeValidator>();
            _pythonValidator = ServiceProvider.GetRequiredService<PythonCodeValidator>();
            _javascriptValidator = ServiceProvider.GetRequiredService<JavaScriptCodeValidator>();
        }

        [Fact]
        public async Task ValidateCode_AllLanguages_ShouldSucceed()
        {
            // Arrange
            var pipeline = ServiceProvider.GetRequiredService<ValidationPipeline>();
            
            var csharpCode = @"
                public class Test {
                    public void Method() {
                        var x = 1;
                    }
                }";

            var pythonCode = @"
                def test_function():
                    x = 1
                    return x";

            var jsCode = @"
                function test() {
                    let x = 1;
                    return x;
                }";

            // Act
            var csharpResult = await pipeline.ValidateCode(csharpCode, "csharp");
            var pythonResult = await pipeline.ValidateCode(pythonCode, "python");
            var jsResult = await pipeline.ValidateCode(jsCode, "javascript");

            // Assert
            csharpResult.IsValid.Should().BeTrue();
            pythonResult.IsValid.Should().BeTrue();
            jsResult.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateCode_WithErrors_ShouldReturnValidationErrors()
        {
            // Arrange
            var pipeline = ServiceProvider.GetRequiredService<ValidationPipeline>();
            
            var invalidCSharpCode = @"
                public class Test {
                    public void Method() {
                        var x = ;  // Syntax error
                    }
                }";

            // Act
            var result = await pipeline.ValidateCode(invalidCSharpCode, "csharp");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidateCode_ResourceIntensive_ShouldManageResourcesCorrectly()
        {
            // Arrange
            var pipeline = ServiceProvider.GetRequiredService<ValidationPipeline>();
            
            var largeCode = new string('x', 1000000); // 1MB of code

            // Act
            var result = await pipeline.ValidateCode(largeCode, "csharp");

            // Assert
            result.ResourceMetrics.Should().NotBeNull();
            result.ResourceMetrics.MemoryUsed.Should().BeLessThan(100 * 1024 * 1024); // Less than 100MB
            result.ResourceMetrics.ProcessingTime.Should().BeLessThan(TimeSpan.FromSeconds(30));
        }
    }
}