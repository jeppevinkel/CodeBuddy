using CodeBuddy.Core.Models;

namespace CodeBuddy.Tests.CodeValidation;

public static class TestUtilities
{
    public static class CodeSamples
    {
        public static class CSharp
        {
            public const string ValidCode = @"
public class Test 
{
    public void Method() 
    {
        var x = 1;
        Console.WriteLine(x);
    }
}";

            public const string InvalidSyntax = @"
public class Test 
{
    public void Method() 
    {
        var x = 1
        Console.WriteLine(x);  // Missing semicolon
    }
}";

            public const string SecurityVulnerability = @"
public class Test 
{
    public void Method(string input) 
    {
        var output = HttpUtility.HtmlEncode(input);  // Potential XSS vulnerability
    }
}";

            public const string StyleViolation = @"
public class test  // Should be PascalCase
{
    public void method()  // Should be PascalCase
    {
    }
}";

            public const string ErrorHandlingViolation = @"
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
        }

        public static class JavaScript
        {
            public const string ValidCode = @"
function test() {
    const x = 1;
    console.log(x);
}";

            public const string InvalidSyntax = @"
function test() {
    const x = 1
    console.log(x);  // Missing semicolon
";  // Missing closing brace

            public const string SecurityVulnerability = @"
function test(input) {
    eval(input);  // Security vulnerability: eval usage
}";

            public const string StyleViolation = @"
function test(){  // Missing space before brace
    var x=1;  // Missing spaces around operator
}";

            public const string ErrorHandlingViolation = @"
function test() {
    try {
        throw new Error();
    } catch(e) {
        // Empty catch block
    }
}";
        }

        public static class Python
        {
            public const string ValidCode = @"
def test():
    x = 1
    print(x)";

            public const string InvalidSyntax = @"
def test()  # Missing colon
    x = 1
    print(x)";

            public const string SecurityVulnerability = @"
def test(input_str):
    result = eval(input_str)  # Security vulnerability: eval usage
    return result";

            public const string StyleViolation = @"
def test():
    x=1  # Missing spaces around operator
    print(x)";

            public const string ErrorHandlingViolation = @"
def test():
    try:
        raise Exception()
    except:  # Bare except clause
        pass";
        }
    }

    public static ValidationOptions CreateValidationOptions(
        bool validateSyntax = true,
        bool validateSecurity = true,
        bool validateStyle = true,
        bool validateBestPractices = true,
        bool validateErrorHandling = true,
        Dictionary<string, object> customRules = null)
    {
        return new ValidationOptions
        {
            ValidateSyntax = validateSyntax,
            ValidateSecurity = validateSecurity,
            ValidateStyle = validateStyle,
            ValidateBestPractices = validateBestPractices,
            ValidateErrorHandling = validateErrorHandling,
            CustomRules = customRules ?? new Dictionary<string, object>()
        };
    }

    public static void AssertValidationResult(
        ValidationResult result,
        bool expectedIsValid,
        string expectedLanguage,
        int expectedTotalIssues,
        int expectedSecurityIssues = 0,
        int expectedStyleIssues = 0,
        int expectedBestPracticeIssues = 0)
    {
        result.IsValid.Should().Be(expectedIsValid);
        result.Language.Should().Be(expectedLanguage);
        result.Statistics.TotalIssues.Should().Be(expectedTotalIssues);
        result.Statistics.SecurityIssues.Should().Be(expectedSecurityIssues);
        result.Statistics.StyleIssues.Should().Be(expectedStyleIssues);
        result.Statistics.BestPracticeIssues.Should().Be(expectedBestPracticeIssues);
    }

    public static void AssertValidationIssue(
        ValidationIssue issue,
        string expectedCode,
        ValidationSeverity expectedSeverity,
        string messageContains = null,
        string locationContains = null,
        string suggestionContains = null)
    {
        issue.Code.Should().Be(expectedCode);
        issue.Severity.Should().Be(expectedSeverity);
        
        if (messageContains != null)
            issue.Message.Should().Contain(messageContains);
            
        if (locationContains != null)
            issue.Location.Should().Contain(locationContains);
            
        if (suggestionContains != null)
            issue.Suggestion.Should().Contain(suggestionContains);
    }
}