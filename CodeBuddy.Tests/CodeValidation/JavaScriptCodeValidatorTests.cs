using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation;

public class JavaScriptCodeValidatorTests
{
    private readonly JavaScriptCodeValidator _validator;
    private readonly Mock<ILogger> _loggerMock;

    public JavaScriptCodeValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _validator = new JavaScriptCodeValidator(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_ValidCode_ReturnsValidResult()
    {
        // Arrange
        var code = @"
            function test() {
                const x = 1;
                return x;
            }";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Language.Should().Be("JavaScript");
    }

    [Fact]
    public async Task ValidateAsync_InvalidSyntax_ReturnsInvalidResult()
    {
        // Arrange
        var code = @"
            function test() {
                const x = 1
                return x  // Missing semicolons
            ";  // Missing closing brace
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task ValidateAsync_SecurityVulnerability_DetectsIssue()
    {
        // Arrange
        var code = @"
            function test(input) {
                eval(input);  // Security vulnerability: eval usage
            }";
        var options = new ValidationOptions { ValidateSecurity = true };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.Statistics.SecurityIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.SecurityVulnerability);
    }

    [Fact]
    public async Task ValidateAsync_StyleViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
            function test(){  // Missing space before brace
                var x=1;  // Missing spaces around operator
            }";
        var options = new ValidationOptions { ValidateStyle = true };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.Statistics.StyleIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("STYLE"));
    }

    [Fact]
    public async Task ValidateAsync_BestPracticeViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
            var x = 1;  // Should use const or let
            function test() {
                return x;
            }";
        var options = new ValidationOptions { ValidateBestPractices = true };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.Statistics.BestPracticeIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("BP"));
    }

    [Fact]
    public async Task ValidateAsync_ErrorHandlingViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
            function test() {
                try {
                    throw new Error();
                } catch(e) {
                    // Empty catch block
                }
            }";
        var options = new ValidationOptions { ValidateErrorHandling = true };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.Issues.Should().Contain(i => i.Code == "EH001");
    }

    [Fact]
    public async Task ValidateAsync_CustomRule_DetectsViolation()
    {
        // Arrange
        var code = @"
            var globalVar = 1;  // Global variable
            function test() {
                return globalVar;
            }";
        var options = new ValidationOptions
        {
            CustomRules = new Dictionary<string, object>
            {
                ["CR001"] = new CustomValidationRule
                {
                    Pattern = "variable",
                    Message = "Global variables are not allowed",
                    Severity = ValidationSeverity.Warning,
                    Suggestion = "Use module-scoped variables instead"
                }
            }
        };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

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
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_MalformedCode_ReturnsInvalidResult()
    {
        // Arrange
        var code = "this is not valid JavaScript @#$%";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_StatisticsCalculation_IsCorrect()
    {
        // Arrange
        var code = @"
            var pass = '123';  // Security issue: hardcoded password
            function test(){  // Style issue: missing space
                try {
                    eval(pass);  // Security issue: eval
                } catch(e) {
                    // Error handling issue: empty catch
                }
            }";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ValidateSecurity = true,
            ValidateErrorHandling = true
        };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

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
            function test(){  // Missing space before brace
            }";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ExcludeRules = new[] { "space-before-blocks" }
        };

        // Act
        var result = await _validator.ValidateAsync(code, "JavaScript", options);

        // Assert
        result.Issues.Should().NotContain(i => i.Code == "space-before-blocks");
    }
}