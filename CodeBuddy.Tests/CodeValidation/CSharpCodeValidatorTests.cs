using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation;

public class CSharpCodeValidatorTests
{
    private readonly CSharpCodeValidator _validator;
    private readonly Mock<ILogger> _loggerMock;

    public CSharpCodeValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _validator = new CSharpCodeValidator(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateAsync_ValidCode_ReturnsValidResult()
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
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Language.Should().Be("C#");
    }

    [Fact]
    public async Task ValidateAsync_InvalidSyntax_ReturnsInvalidResult()
    {
        // Arrange
        var code = @"
            public class Test 
            {
                public void Method() 
                {
                    var x = 1  // Missing semicolon
                }
            }";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.Error);
        result.Issues.Should().Contain(i => i.Message.Contains("semicolon"));
    }

    [Fact]
    public async Task ValidateAsync_SecurityVulnerability_DetectsIssue()
    {
        // Arrange
        var code = @"
            using System.Web;
            public class Test 
            {
                public void Method(string input) 
                {
                    var output = HttpUtility.HtmlEncode(input); // Potential XSS vulnerability
                }
            }";
        var options = new ValidationOptions { ValidateSecurity = true };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.Statistics.SecurityIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Severity == ValidationSeverity.SecurityVulnerability);
    }

    [Fact]
    public async Task ValidateAsync_StyleViolation_DetectsIssue()
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
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.Statistics.StyleIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("STYLE"));
    }

    [Fact]
    public async Task ValidateAsync_BestPracticeViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
            public class Test 
            {
                public void Method() 
                {
                    var list = new List<string>();
                    for(int i = 0; i < list.Count; i++)  // Should use foreach
                    {
                        Console.WriteLine(list[i]);
                    }
                }
            }";
        var options = new ValidationOptions { ValidateBestPractices = true };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.Statistics.BestPracticeIssues.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Code.StartsWith("BP"));
    }

    [Fact]
    public async Task ValidateAsync_ErrorHandlingViolation_DetectsIssue()
    {
        // Arrange
        var code = @"
            public class Test 
            {
                public void Method() 
                {
                    try
                    {
                        throw new Exception();
                    }
                    catch  // Empty catch block
                    {
                    }
                }
            }";
        var options = new ValidationOptions { ValidateErrorHandling = true };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.Issues.Should().Contain(i => i.Code == "EH001");
    }

    [Fact]
    public async Task ValidateAsync_CustomRule_DetectsViolation()
    {
        // Arrange
        var code = @"
            public class Test 
            {
                private string _field;  // Custom rule: no private fields
            }";
        var options = new ValidationOptions
        {
            CustomRules = new Dictionary<string, object>
            {
                ["CR001"] = new CustomValidationRule
                {
                    Pattern = "field",
                    Condition = node => node is Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax field 
                        && field.Modifiers.Any(m => m.ValueText == "private"),
                    Message = "Private fields are not allowed",
                    Severity = ValidationSeverity.Warning,
                    Suggestion = "Use properties instead of private fields"
                }
            }
        };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

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
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_MalformedCode_ReturnsInvalidResult()
    {
        // Arrange
        var code = "this is not valid C# code @#$%";
        var options = new ValidationOptions();

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_StatisticsCalculation_IsCorrect()
    {
        // Arrange
        var code = @"
            public class test  // Style issue: should be PascalCase
            {
                public void Method()
                {
                    try
                    {
                        var password = ""123"";  // Security issue: hardcoded password
                    }
                    catch  // Error handling issue: empty catch
                    {
                    }
                }
            }";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ValidateSecurity = true,
            ValidateErrorHandling = true
        };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

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
            public class test  // Style issue: should be PascalCase
            {
            }";
        var options = new ValidationOptions
        {
            ValidateStyle = true,
            ExcludeRules = new[] { "SA1300" }  // Exclude Pascal case rule
        };

        // Act
        var result = await _validator.ValidateAsync(code, "C#", options);

        // Assert
        result.Issues.Should().NotContain(i => i.Code == "SA1300");
    }
}