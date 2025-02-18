using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Models.Analytics;
using CodeBuddy.Core.Models.Errors;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.ErrorHandling
{
    public class ErrorAnalyticsServiceTests
    {
        private readonly Mock<ITimeSeriesStorage> _timeSeriesStorageMock;
        private readonly Mock<IErrorHandlingService> _errorHandlingServiceMock;
        private readonly ErrorAnalyticsService _service;

        public ErrorAnalyticsServiceTests()
        {
            _timeSeriesStorageMock = new Mock<ITimeSeriesStorage>();
            _errorHandlingServiceMock = new Mock<IErrorHandlingService>();
            _service = new ErrorAnalyticsService(_timeSeriesStorageMock.Object, _errorHandlingServiceMock.Object);
        }

        [Fact]
        public async Task RecordError_ShouldStoreErrorMetrics()
        {
            // Arrange
            var error = new Exception("Test error");
            var context = new ErrorRecoveryContext
            {
                Strategy = new RetryStrategy(),
                RecoverySuccessful = true,
                RecoveryDuration = TimeSpan.FromSeconds(1),
                ResourceMetrics = new Dictionary<string, double>()
            };

            // Act
            await _service.RecordError(error, context);

            // Assert
            _timeSeriesStorageMock.Verify(
                x => x.StoreMetrics(
                    It.Is<string>(s => s == "errors"),
                    It.Is<Dictionary<string, object>>(d => 
                        d["type"].ToString() == error.GetType().Name &&
                        d["message"].ToString() == error.Message)),
                Times.Once);
        }

        [Fact]
        public async Task AnalyzeErrorPatterns_ShouldIdentifyFrequencyPatterns()
        {
            // Arrange
            var timeWindow = DateTime.UtcNow.AddHours(-1);
            var errorMetrics = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "type", "ArgumentException" } },
                new Dictionary<string, object> { { "type", "ArgumentException" } },
                new Dictionary<string, object> { { "type", "NullReferenceException" } }
            };

            _timeSeriesStorageMock.Setup(x => x.GetMetrics("errors", timeWindow, It.IsAny<DateTime>()))
                .ReturnsAsync(errorMetrics);

            // Act
            var patterns = await _service.AnalyzeErrorPatterns(timeWindow);

            // Assert
            Assert.Equal(2, patterns.Count);
            var argumentExceptionPattern = patterns.First(p => p.ErrorType == "ArgumentException");
            Assert.Equal(2, argumentExceptionPattern.Frequency);
        }

        [Fact]
        public async Task EvaluateRecoveryStrategies_ShouldCalculateMetrics()
        {
            // Arrange
            var metrics = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> 
                { 
                    { "recovery_strategy", "RetryStrategy" },
                    { "type", "NetworkException" },
                    { "recovery_successful", true }
                },
                new Dictionary<string, object> 
                { 
                    { "recovery_strategy", "RetryStrategy" },
                    { "type", "NetworkException" },
                    { "recovery_successful", false }
                }
            };

            _timeSeriesStorageMock.Setup(x => x.GetMetrics("errors", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(metrics);

            // Act
            var strategies = await _service.EvaluateRecoveryStrategies();

            // Assert
            Assert.Single(strategies);
            var strategy = strategies.First();
            Assert.Equal("RetryStrategy", strategy.StrategyName);
            Assert.Equal("NetworkException", strategy.ErrorCategory);
            Assert.Equal(0.5, strategy.SuccessRate);
        }

        [Fact]
        public async Task AnalyzeCircuitBreakerPatterns_ShouldIdentifyTransitions()
        {
            // Arrange
            var transitions = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> 
                { 
                    { "service", "UserService" },
                    { "from_state", "Closed" },
                    { "to_state", "Open" }
                }
            };

            _timeSeriesStorageMock.Setup(x => x.GetMetrics("circuit_breaker", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(transitions);

            // Act
            var patterns = await _service.AnalyzeCircuitBreakerPatterns();

            // Assert
            Assert.Single(patterns);
            var pattern = patterns.First();
            Assert.Equal("UserService", pattern.ServiceName);
            Assert.Equal(1, pattern.TotalTransitions);
        }

        [Fact]
        public async Task DetectErrorSequences_ShouldIdentifyPatterns()
        {
            // Arrange
            var errors = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "type", "DatabaseException" } },
                new Dictionary<string, object> { { "type", "ConnectionException" } },
                new Dictionary<string, object> { { "type", "TimeoutException" } }
            };

            _timeSeriesStorageMock.Setup(x => x.GetMetrics("errors", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(errors);

            // Act
            var sequences = await _service.DetectErrorSequences();

            // Assert
            Assert.NotEmpty(sequences);
        }

        [Fact]
        public async Task GenerateAnalyticsReport_ShouldIncludeAllMetrics()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-1);
            var endTime = DateTime.UtcNow;

            // Act
            var report = await _service.GenerateAnalyticsReport(startTime, endTime);

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.FrequencyPatterns);
            Assert.NotNull(report.StrategyMetrics);
            Assert.NotNull(report.CircuitBreakerStats);
            Assert.NotNull(report.DetectedPatterns);
            Assert.NotNull(report.Recommendations);
        }
    }
}