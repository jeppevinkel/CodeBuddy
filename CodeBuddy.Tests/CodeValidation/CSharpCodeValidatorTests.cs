using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class CSharpCodeValidatorTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IPerformanceMonitor> _performanceMonitorMock;
        private readonly CSharpCodeValidator _validator;

        public CSharpCodeValidatorTests()
        {
            _loggerMock = new Mock<ILogger>();
            _performanceMonitorMock = new Mock<IPerformanceMonitor>();
            _validator = new CSharpCodeValidator(_loggerMock.Object, _performanceMonitorMock.Object);
        }

        [Fact]
        public void ValidateCode_ValidCSharpCode_ShouldReturnValidResult()
        {
            // Arrange
            var code = @"
                public class Test 
                {
                    public void Method() 
                    {
                        var x = 1;
                    }
                }";
            var options = new ValidationOptions 
            { 
                ValidateSyntax = true,
                ValidateStyle = true
            };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public void ValidateCode_InvalidSyntax_ShouldReturnInvalidResult()
        {
            // Arrange
            var code = @"
                public class Test 
                {
                    public void Method()
                    {
                        var x = 1    // Missing semicolon
                    }
                }";
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.Syntax);
        }

        [Fact]
        public void ValidateCode_SecurityVulnerability_ShouldReportSecurityIssue()
        {
            // Arrange
            var code = @"
                public class Test 
                {
                    public void Method()
                    {
                        System.Web.HttpContext.Current.Response.Write(userInput); // XSS vulnerability
                    }
                }";
            var options = new ValidationOptions { ValidateSecurity = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.Security);
        }

        [Fact]
        public void ValidateCode_StyleViolation_ShouldReportStyleIssue()
        {
            // Arrange
            var code = @"
                public class test  // Class name should be PascalCase
                {
                    public void method()  // Method name should be PascalCase
                    {
                    }
                }";
            var options = new ValidationOptions { ValidateStyle = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.Style);
        }

        [Fact]
        public void ValidateCode_PerformanceIssue_ShouldReportPerformanceWarning()
        {
            // Arrange
            var code = @"
                public class Test 
                {
                    public void Method()
                    {
                        for(int i = 0; i < 1000000; i++) 
                        {
                            string s = s + i.ToString(); // Inefficient string concatenation
                        }
                    }
                }";
            var options = new ValidationOptions { ValidatePerformance = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.True(result.Issues.Any(i => i.Type == ValidationIssueType.Performance));
        }

        [Fact]
        public void ValidateCode_WithConcurrency_ShouldHandleMultipleValidations()
        {
            // Arrange
            var code = "public class Test { }";
            var options = new ValidationOptions();
            var tasks = new List<Task<ValidationResult>>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => _validator.ValidateCode(code, options)));
            }
            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.All(tasks, t => Assert.True(t.Result.IsValid));
            _performanceMonitorMock.Verify(x => x.StartMeasurement(), Times.Exactly(10));
        }

        [Fact]
        public void ValidateCode_ExceedsPerformanceThreshold_ShouldLogWarning()
        {
            // Arrange
            var code = @"
                public class Test 
                {
                    public void Method()
                    {
                        Thread.Sleep(1000); // Simulating slow code
                    }
                }";
            var options = new ValidationOptions 
            { 
                ValidatePerformance = true,
                PerformanceThresholdMs = 500
            };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            _loggerMock.Verify(x => x.Log(It.Is<string>(s => 
                s.Contains("Performance threshold exceeded"))), Times.Once);
        }
    }
}