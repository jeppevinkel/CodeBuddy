using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation;

public class PythonCodeValidatorTests
{
    private readonly PythonCodeValidator _validator;
    private readonly Mock<ILogger> _loggerMock;

    public PythonCodeValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _validator = new PythonCodeValidator(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_ValidCode_ReturnsValidResult()
    {
        // Arrange
        var code = @"
def test():
    x = 1
    return x";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Language.Should().Be("Python");
    }

    [Fact]
    public async Task ValidateAsync_InvalidSyntax_ReturnsInvalidResult()
    {
        // Arrange
        var code = @"
def test()  # Missing colon
    x = 1
    return x";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task ValidateAsync_SecurityVulnerability_DetectsIssue()
    {
        // Arrange
        var code = @"
def test(input_str):
    result = eval(input_str)  # Security vulnerability: eval usage
    return result";
        var options = new ValidationOptions { ValidateSecurity = true };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Statistics.SecurityIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.SecurityVulnerability);
    }

    [Fact]
    public async Task ValidateAsync_StyleViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
def test():
    x=1  # Missing spaces around operator
    return x";
        var options = new ValidationOptions { ValidateStyle = true };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Statistics.StyleIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("STYLE"));
    }

    [Fact]
    public async Task ValidateAsync_BestPracticeViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
x = 1  # Global variable
def test():
    global x
    return x";
        var options = new ValidationOptions { ValidateBestPractices = true };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Statistics.BestPracticeIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("BP"));
    }

    [Fact]
    public async Task ValidateAsync_ErrorHandlingViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
def test():
    try:
        raise Exception()
    except:  # Bare except clause
        pass";
        var options = new ValidationOptions { ValidateErrorHandling = true };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Issues.Should().Contain(i => i.Code == "EH001");
    }

    [Fact]
    public async Task ValidateAsync_CustomRule_DetectsViolation()
    {
        // Arrange
        var code = @"
def test():
    print('debug')  # Custom rule: no print statements";
        var options = new ValidationOptions
        {
            CustomRules = new Dictionary<string, object>
            {
                ["CR001"] = new CustomValidationRule
                {
                    Pattern = "print",
                    Message = "Print statements are not allowed in production code",
                    Severity = ValidationSeverity.Warning,
                    Suggestion = "Use proper logging instead of print statements"
                }
            }
        };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Issues.Should().Contain(i => i.Code == "CR001");
    }

    [Fact]
    public async Task ValidateAsync_EmptyInput_ReturnsValidResult()
    {
        // Arrange
        var code = "";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_MalformedCode_ReturnsInvalidResult()
    {
        // Arrange
        var code = "this is not valid Python @#$%";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_StatisticsCalculation_IsCorrect()
    {
        // Arrange
        var code = @"
password = '123'  # Security issue: hardcoded password
def test():
    x=1  # Style issue: missing spaces
    try:
        eval(password)  # Security issue: eval
    except:  # Error handling issue: bare except
        pass";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ValidateSecurity = true,
            ValidateErrorHandling = true
        };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Statistics.TotalIssues.Should().Be(result.Issues.Count);
        result.Statistics.SecurityIssues.Should().BeGreaterThan(0);
        result.Statistics.StyleIssues.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateAsync_ExcludedRules_AreNotChecked()
    {
        // Arrange
        var code = @"
def test():
    x=1  # Missing spaces around operator";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ExcludeRules = new[] { "E225" }  // Exclude missing spaces around operator rule
        };

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.Issues.Should().NotContain(i => i.Code == "E225");
    }

    [Fact]
    public async Task ValidateAsync_IndentationError_DetectsIssue()
    {
        // Arrange
        var code = @"
def test():
return x  # Incorrect indentation";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "Python", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Message.Contains("indentation"));
    }

    [Fact]
    public async Task ValidateAsync_PerformanceBenchmark_CompletesInTime()
    {
        // Arrange
        var code = new string('x', 10000);  // Large code sample
        var options = new ValidationOptions();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _validator.ValidateAsync(code, "Python", options);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        duration.TotalSeconds.Should().BeLessThan(5);  // Should complete within 5 seconds
    }
}