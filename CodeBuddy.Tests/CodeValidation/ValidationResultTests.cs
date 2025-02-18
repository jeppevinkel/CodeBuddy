using System;
using System.Linq;
using CodeBuddy.Core.Models;
using CodeBuddy.Core.Models.AST;
using CodeBuddy.Core.Models.Errors;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ValidationResultTests
    {
        [Fact]
        public void Constructor_InitializesCollectionsAndDefaultStatus()
        {
            // Act
            var result = new ValidationResult();

            // Assert
            Assert.NotNull(result.Issues);
            Assert.NotNull(result.PatternMatches);
            Assert.NotNull(result.Errors);
            Assert.Empty(result.Issues);
            Assert.Empty(result.PatternMatches);
            Assert.Equal(ValidationStatus.Success, result.Status);
        }

        [Fact]
        public void HasErrors_WithErrorSeverityIssue_ReturnsTrue()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Error,
                Message = "Test error"
            });

            // Act
            bool hasErrors = result.HasErrors;

            // Assert
            Assert.True(hasErrors);
        }

        [Fact]
        public void HasErrors_WithCriticalSeverityIssue_ReturnsTrue()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Critical,
                Message = "Test critical error"
            });

            // Act
            bool hasErrors = result.HasErrors;

            // Assert
            Assert.True(hasErrors);
        }

        [Fact]
        public void HasErrors_WithWarningAndInfoIssues_ReturnsFalse()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Warning,
                Message = "Test warning"
            });
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Info,
                Message = "Test info"
            });

            // Act
            bool hasErrors = result.HasErrors;

            // Assert
            Assert.False(hasErrors);
        }

        [Fact]
        public void HasWarnings_WithWarningIssue_ReturnsTrue()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Warning,
                Message = "Test warning"
            });

            // Act
            bool hasWarnings = result.HasWarnings;

            // Assert
            Assert.True(hasWarnings);
        }

        [Fact]
        public void HasWarnings_WithErrorInErrorCollection_ReturnsFalse()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError(new ValidationError 
            { 
                Severity = ErrorSeverity.Error,
                Message = "Test error"
            });

            // Act
            bool hasWarnings = result.HasWarnings;

            // Assert
            Assert.False(hasWarnings);
        }

        [Fact]
        public void GetIssuesBySeverity_ReturnsMatchingIssues()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Warning,
                Message = "Warning 1"
            });
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Warning,
                Message = "Warning 2"
            });
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Error,
                Message = "Error 1"
            });

            // Act
            var warnings = result.GetIssuesBySeverity(IssueSeverity.Warning).ToList();

            // Assert
            Assert.Equal(2, warnings.Count);
            Assert.All(warnings, w => Assert.Equal(IssueSeverity.Warning, w.Severity));
        }

        [Fact]
        public void GetIssuesBySeverity_WithNoMatchingIssues_ReturnsEmptyCollection()
        {
            // Arrange
            var result = new ValidationResult();
            result.Issues.Add(new ValidationIssue 
            { 
                Severity = IssueSeverity.Error,
                Message = "Error 1"
            });

            // Act
            var criticalIssues = result.GetIssuesBySeverity(IssueSeverity.Critical);

            // Assert
            Assert.Empty(criticalIssues);
        }

        [Fact]
        public void AddError_UpdatesStatusToError_WhenCriticalErrorAdded()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddError(new ValidationError 
            { 
                Severity = ErrorSeverity.Critical,
                Message = "Critical error"
            });

            // Assert
            Assert.Equal(ValidationStatus.Error, result.Status);
        }

        [Fact]
        public void AddError_UpdatesStatusToWarning_WhenWarningErrorAdded()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddError(new ValidationError 
            { 
                Severity = ErrorSeverity.Warning,
                Message = "Warning error"
            });

            // Assert
            Assert.Equal(ValidationStatus.Warning, result.Status);
        }

        [Fact]
        public void AddError_MaintainsErrorStatus_WhenWarningAddedAfterError()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError(new ValidationError 
            { 
                Severity = ErrorSeverity.Error,
                Message = "Error"
            });

            // Act
            result.AddError(new ValidationError 
            { 
                Severity = ErrorSeverity.Warning,
                Message = "Warning"
            });

            // Assert
            Assert.Equal(ValidationStatus.Error, result.Status);
        }

        [Fact]
        public void ValidationResult_HandlesConcurrentAdditions()
        {
            // Arrange
            var result = new ValidationResult();
            var errors = Enumerable.Range(0, 1000).Select(i => new ValidationError 
            { 
                Severity = ErrorSeverity.Warning,
                Message = $"Warning {i}"
            }).ToList();

            // Act
            Parallel.ForEach(errors, error => result.AddError(error));

            // Assert
            Assert.Equal(1000, result.Errors.Count);
            Assert.Equal(ValidationStatus.Warning, result.Status);
        }

        [Fact]
        public void ValidationResult_HandlesNullValuesInCollections()
        {
            // Arrange
            var result = new ValidationResult();
            
            // Act & Assert - Should not throw exceptions
            result.Issues.Add(null);
            result.PatternMatches.Add(null);
            result.Errors.AddError(null);

            Assert.DoesNotContain(null, result.Issues);
            Assert.DoesNotContain(null, result.PatternMatches);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void GetIssuesBySeverity_PerformanceTest(int issueCount)
        {
            // Arrange
            var result = new ValidationResult();
            for (int i = 0; i < issueCount; i++)
            {
                result.Issues.Add(new ValidationIssue 
                { 
                    Severity = i % 2 == 0 ? IssueSeverity.Warning : IssueSeverity.Error,
                    Message = $"Issue {i}"
                });
            }

            // Act
            var startTime = DateTime.Now;
            var warnings = result.GetIssuesBySeverity(IssueSeverity.Warning).ToList();
            var endTime = DateTime.Now;

            // Assert
            Assert.Equal(issueCount / 2, warnings.Count);
            Assert.True((endTime - startTime).TotalMilliseconds < 1000, 
                $"Performance test failed. Processing {issueCount} items took more than 1 second");
        }
    }
}