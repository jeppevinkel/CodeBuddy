using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation;

public class BaseCodeValidatorTests
{
    private class TestCodeValidator : BaseCodeValidator
    {
        public bool SyntaxValidated { get; private set; }
        public bool SecurityValidated { get; private set; }
        public bool StyleValidated { get; private set; }
        public bool BestPracticesValidated { get; private set; }
        public bool ErrorHandlingValidated { get; private set; }
        public bool CustomRulesValidated { get; private set; }

        public TestCodeValidator(ILogger logger) : base(logger) { }

        protected override Task ValidateSyntaxAsync(string code, ValidationResult result)
        {
            SyntaxValidated = true;
            return Task.CompletedTask;
        }

        protected override Task ValidateSecurityAsync(string code, ValidationResult result)
        {
            SecurityValidated = true;
            return Task.CompletedTask;
        }

        protected override Task ValidateStyleAsync(string code, ValidationResult result)
        {
            StyleValidated = true;
            return Task.CompletedTask;
        }

        protected override Task ValidateBestPracticesAsync(string code, ValidationResult result)
        {
            BestPracticesValidated = true;
            return Task.CompletedTask;
        }

        protected override Task ValidateErrorHandlingAsync(string code, ValidationResult result)
        {
            ErrorHandlingValidated = true;
            return Task.CompletedTask;
        }

        protected override Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules)
        {
            CustomRulesValidated = true;
            return Task.CompletedTask;
        }
    }

    private readonly TestCodeValidator _validator;
    private readonly Mock<ILogger> _loggerMock;

    public BaseCodeValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _validator = new TestCodeValidator(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_AllValidationsEnabled_CallsAllValidationMethods()
    {
        // Arrange
        var code = "test code";
        var options = new ValidationOptions
        {
            ValidateSyntax = true,
            ValidateSecurity = true,
            ValidateStyle = true,
            ValidateBestPractices = true,
            ValidateErrorHandling = true,
            CustomRules = new Dictionary<string, object> { ["test"] = new object() }
        };

        // Act
        await _validator.ValidateAsync(code, "TestLang", options);

        // Assert
        _validator.SyntaxValidated.Should().BeTrue();
        _validator.SecurityValidated.Should().BeTrue();
        _validator.StyleValidated.Should().BeTrue();
        _validator.BestPracticesValidated.Should().BeTrue();
        _validator.ErrorHandlingValidated.Should().BeTrue();
        _validator.CustomRulesValidated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_SelectiveValidations_OnlyCallsSelectedValidations()
    {
        // Arrange
        var code = "test code";
        var options = new ValidationOptions
        {
            ValidateSyntax = true,
            ValidateSecurity = false,
            ValidateStyle = true,
            ValidateBestPractices = false,
            ValidateErrorHandling = true,
            CustomRules = new Dictionary<string, object>()
        };

        // Act
        await _validator.ValidateAsync(code, "TestLang", options);

        // Assert
        _validator.SyntaxValidated.Should().BeTrue();
        _validator.SecurityValidated.Should().BeFalse();
        _validator.StyleValidated.Should().BeTrue();
        _validator.BestPracticesValidated.Should().BeFalse();
        _validator.ErrorHandlingValidated.Should().BeTrue();
        _validator.CustomRulesValidated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ValidationError_LogsAndReturnsError()
    {
        // Arrange
        var code = "test code";
        var options = new ValidationOptions();
        var exception = new Exception("Test error");

        var mockValidator = new Mock<BaseCodeValidator>(_loggerMock.Object);
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ValidationOptions>()))
            .Throws(exception);

        // Act
        var result = await mockValidator.Object.ValidateAsync(code, "TestLang", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "VAL001" && i.Message.Contains("Test error"));
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_StatisticsCalculation_IsAccurate()
    {
        // Arrange
        var code = "test code";
        var options = new ValidationOptions();
        var mockValidator = new Mock<BaseCodeValidator>(_loggerMock.Object);
        
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ValidationOptions>()))
            .CallBase();
        
        mockValidator.Protected()
            .Setup<Task>("ValidateSecurityAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<ValidationResult>())
            .Callback<string, ValidationResult>((_, result) => 
            {
                result.Issues.Add(new ValidationIssue 
                { 
                    Severity = ValidationSeverity.SecurityVulnerability,
                    Code = "SEC001"
                });
            })
            .Returns(Task.CompletedTask);

        mockValidator.Protected()
            .Setup<Task>("ValidateStyleAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<ValidationResult>())
            .Callback<string, ValidationResult>((_, result) => 
            {
                result.Issues.Add(new ValidationIssue 
                { 
                    Severity = ValidationSeverity.Warning,
                    Code = "STYLE001"
                });
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await mockValidator.Object.ValidateAsync(code, "TestLang", options);

        // Assert
        result.Statistics.TotalIssues.Should().Be(2);
        result.Statistics.SecurityIssues.Should().Be(1);
        result.Statistics.StyleIssues.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_CustomRules_AreProcessed()
    {
        // Arrange
        var code = "test code";
        var customRule = new CustomValidationRule
        {
            Pattern = "test",
            Message = "Test message",
            Severity = ValidationSeverity.Warning
        };
        var options = new ValidationOptions
        {
            CustomRules = new Dictionary<string, object>
            {
                ["CR001"] = customRule
            }
        };

        // Act
        var result = await _validator.ValidateAsync(code, "TestLang", options);

        // Assert
        _validator.CustomRulesValidated.Should().BeTrue();
    }
}