using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class JavaScriptCodeValidatorTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IPerformanceMonitor> _performanceMonitorMock;
        private readonly JavaScriptCodeValidator _validator;

        public JavaScriptCodeValidatorTests()
        {
            _loggerMock = new Mock<ILogger>();
            _performanceMonitorMock = new Mock<IPerformanceMonitor>();
            _validator = new JavaScriptCodeValidator(_loggerMock.Object, _performanceMonitorMock.Object);
        }

        [Fact]
        public void ValidateCode_ValidJavaScriptCode_ShouldReturnValidResult()
        {
            // Arrange
            var code = @"
                function test() {
                    const x = 1;
                    return x + 2;
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
                function test() {
                    const x = 1    // Missing semicolon
                    return x + 2
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
                function test() {
                    eval(userInput); // Dangerous eval usage
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
                function test(){  // Missing space before brace
                    var x=1;     // Missing spaces around operator
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
                function test() {
                    let arr = [];
                    for(let i = 0; i < 1000000; i++) {
                        arr.push(i);  // Inefficient array growth
                    }
                }";
            var options = new ValidationOptions { ValidatePerformance = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.Performance);
        }

        [Fact]
        public void ValidateCode_BestPracticesViolation_ShouldReportBestPracticesIssue()
        {
            // Arrange
            var code = @"
                var x = 1;  // Using var instead of const/let
                function test() {
                    console.log(x);  // Using global variable
                }";
            var options = new ValidationOptions { ValidateBestPractices = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.BestPractices);
        }

        [Fact]
        public void ValidateCode_ResourceIntensiveOperation_ShouldMeasureResourceUsage()
        {
            // Arrange
            var code = @"
                function test() {
                    let result = '';
                    for(let i = 0; i < 100000; i++) {
                        result += i.toString();  // Memory-intensive operation
                    }
                }";
            var options = new ValidationOptions 
            { 
                ValidatePerformance = true,
                MeasureResourceUsage = true
            };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            _performanceMonitorMock.Verify(x => x.GetMetrics(), Times.AtLeastOnce);
            Assert.True(result.ResourceUsage.MemoryUsage > 0);
        }

        [Fact]
        public void ValidateCode_WithCustomRules_ShouldApplyCustomValidations()
        {
            // Arrange
            var code = @"
                function test() {
                    // TODO: Implement this
                    throw 'Not implemented';
                }";
            var options = new ValidationOptions 
            { 
                CustomRules = new[] 
                { 
                    new ValidationRule 
                    { 
                        Name = "No TODOs",
                        Severity = ValidationSeverity.Warning
                    }
                }
            };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.Contains(result.Issues, i => i.Rule.Name == "No TODOs");
        }
    }
}