using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class BaseCodeValidatorTests
    {
        private class TestCodeValidator : BaseCodeValidator
        {
            public TestCodeValidator(ILogger logger, IPerformanceMonitor performanceMonitor) 
                : base(logger, performanceMonitor)
            {
            }

            public override ValidationResult ValidateCode(string code, ValidationOptions options)
            {
                // Simple implementation for testing base functionality
                BeginValidation();
                var result = new ValidationResult 
                { 
                    IsValid = true,
                    ValidationTime = 100,
                    Issues = new List<ValidationIssue>()
                };
                EndValidation();
                return result;
            }
        }

        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IPerformanceMonitor> _performanceMonitorMock;
        private readonly TestCodeValidator _validator;

        public BaseCodeValidatorTests()
        {
            _loggerMock = new Mock<ILogger>();
            _performanceMonitorMock = new Mock<IPerformanceMonitor>();
            _validator = new TestCodeValidator(_loggerMock.Object, _performanceMonitorMock.Object);
        }

        [Fact]
        public void BeginValidation_ShouldInitializePerformanceMonitoring()
        {
            // Arrange
            _performanceMonitorMock.Setup(x => x.StartMeasurement()).Verifiable();

            // Act
            var result = _validator.ValidateCode("test code", new ValidationOptions());

            // Assert
            _performanceMonitorMock.Verify(x => x.StartMeasurement(), Times.Once);
        }

        [Fact]
        public void EndValidation_ShouldStopPerformanceMonitoring()
        {
            // Arrange
            _performanceMonitorMock.Setup(x => x.StopMeasurement()).Verifiable();

            // Act
            var result = _validator.ValidateCode("test code", new ValidationOptions());

            // Assert
            _performanceMonitorMock.Verify(x => x.StopMeasurement(), Times.Once);
        }

        [Fact]
        public void ValidateCode_ShouldLogValidationStart()
        {
            // Arrange
            _loggerMock.Setup(x => x.Log(It.IsAny<string>())).Verifiable();

            // Act
            var result = _validator.ValidateCode("test code", new ValidationOptions());

            // Assert
            _loggerMock.Verify(x => x.Log(It.Is<string>(s => s.Contains("Starting code validation"))), Times.Once);
        }

        [Fact]
        public void ValidateCode_WithNullCode_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateCode(null, new ValidationOptions()));
        }

        [Fact]
        public void ValidateCode_WithNullOptions_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.ValidateCode("test code", null));
        }

        [Fact]
        public void PerformanceMetrics_ShouldBeCollectedDuringValidation()
        {
            // Arrange
            var metrics = new PerformanceMetrics { ExecutionTime = 100, MemoryUsage = 1024 };
            _performanceMonitorMock.Setup(x => x.GetMetrics()).Returns(metrics);

            // Act
            var result = _validator.ValidateCode("test code", new ValidationOptions());

            // Assert
            Assert.Equal(metrics.ExecutionTime, result.ValidationTime);
            _performanceMonitorMock.Verify(x => x.GetMetrics(), Times.Once);
        }
    }
}