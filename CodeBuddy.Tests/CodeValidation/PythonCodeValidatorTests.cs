using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class PythonCodeValidatorTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IPerformanceMonitor> _performanceMonitorMock;
        private readonly PythonCodeValidator _validator;

        public PythonCodeValidatorTests()
        {
            _loggerMock = new Mock<ILogger>();
            _performanceMonitorMock = new Mock<IPerformanceMonitor>();
            _validator = new PythonCodeValidator(_loggerMock.Object, _performanceMonitorMock.Object);
        }

        [Fact]
        public void ValidateCode_ValidPythonCode_ShouldReturnValidResult()
        {
            // Arrange
            var code = @"
def test_function():
    x = 1
    return x + 2";
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
def test_function()    # Missing colon
    x = 1
    return x + 2";
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
def test_function():
    exec(user_input)  # Dangerous exec usage";
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
def TestFunction():  # Function name should be snake_case
    x=1  # Missing spaces around operator
    return x";
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
def test_function():
    result = ''
    for i in range(1000000):
        result += str(i)  # Inefficient string concatenation";
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
# Missing docstring
def test_function():
    global_var = 10  # Using global variable
    return global_var";
            var options = new ValidationOptions { ValidateBestPractices = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.Contains(result.Issues, i => i.Type == ValidationIssueType.BestPractices);
        }

        [Fact]
        public void ValidateCode_IndentationError_ShouldReportSyntaxIssue()
        {
            // Arrange
            var code = @"
def test_function():
x = 1  # Incorrect indentation
    return x";
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => 
                i.Type == ValidationIssueType.Syntax && 
                i.Description.Contains("indentation"));
        }

        [Fact]
        public void ValidateCode_ComplexityThreshold_ShouldReportComplexityIssue()
        {
            // Arrange
            var code = @"
def test_function():
    for i in range(10):
        for j in range(10):
            for k in range(10):
                if i > 0:
                    if j > 0:
                        if k > 0:
                            return True";
            var options = new ValidationOptions 
            { 
                ValidateComplexity = true,
                ComplexityThreshold = 5
            };

            // Act
            var result = _validator.ValidateCode(code, options);

            // Assert
            Assert.Contains(result.Issues, i => 
                i.Type == ValidationIssueType.Complexity);
        }

        [Fact]
        public void ValidateCode_WithCustomRules_ShouldApplyCustomValidations()
        {
            // Arrange
            var code = @"
def test_function():
    # TODO: Implement this
    raise NotImplementedError()";
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