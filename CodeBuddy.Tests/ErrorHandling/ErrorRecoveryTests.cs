using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Models.Errors;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.ErrorHandling
{
    public class ErrorRecoveryTests
    {
        private readonly Mock<IErrorAnalyticsService> _analyticsMock;
        private readonly RetryPolicy _retryPolicy;

        public ErrorRecoveryTests()
        {
            _analyticsMock = new Mock<IErrorAnalyticsService>();
            _retryPolicy = new RetryPolicy
            {
                MaxRetryAttempts = new Dictionary<ErrorCategory, int>
                {
                    { ErrorCategory.Resource, 3 },
                    { ErrorCategory.System, 3 }
                },
                BaseDelayMs = 100,
                MaxDelayMs = 1000,
                CircuitBreakerThreshold = 3,
                CircuitBreakerResetMs = 5000
            };
        }

        [Fact]
        public async Task NetworkTimeoutStrategy_ShouldRetry_WhenNetworkRecovers()
        {
            // Arrange
            var strategy = new NetworkTimeoutRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            var error = new ValidationError
            {
                Category = ErrorCategory.System,
                ErrorCode = "NET_TIMEOUT",
                Message = "Network timeout occurred"
            };
            var context = ErrorRecoveryContext.Create(error);

            // Act
            var canHandle = strategy.CanHandle(error);
            var result = await strategy.AttemptRecoveryAsync(context);

            // Assert
            Assert.True(canHandle);
            Assert.True(result); // Assuming network is available in test environment
        }

        [Fact]
        public async Task ResourceExhaustionStrategy_ShouldHandle_MemoryErrors()
        {
            // Arrange
            var strategy = new ResourceExhaustionRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            var error = new ResourceError
            {
                Category = ErrorCategory.Resource,
                ResourceType = "memory",
                Message = "Out of memory"
            };
            var context = ErrorRecoveryContext.Create(error);

            // Act
            var canHandle = strategy.CanHandle(error);
            var result = await strategy.AttemptRecoveryAsync(context);

            // Assert
            Assert.True(canHandle);
            Assert.True(result);
        }

        [Fact]
        public async Task CircuitBreaker_ShouldOpen_AfterThresholdExceeded()
        {
            // Arrange
            var circuitBreaker = new CircuitBreaker(2, TimeSpan.FromSeconds(5));
            var failingTask = new Func<Task<bool>>(() => Task.FromException<bool>(new Exception("Test")));

            // Act & Assert
            for (int i = 0; i < 2; i++)
            {
                await Assert.ThrowsAsync<Exception>(() => 
                    circuitBreaker.ExecuteAsync(failingTask, "test"));
            }

            // Circuit should be open now
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => 
                circuitBreaker.ExecuteAsync(failingTask, "test"));
        }

        [Fact]
        public async Task ErrorRecoveryOrchestrator_ShouldUseAppropriateStrategy()
        {
            // Arrange
            var networkStrategy = new NetworkTimeoutRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            var resourceStrategy = new ResourceExhaustionRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            
            var orchestrator = new ErrorRecoveryOrchestrator(
                new[] { networkStrategy, resourceStrategy },
                _analyticsMock.Object,
                _retryPolicy);

            var error = new ValidationError
            {
                Category = ErrorCategory.System,
                ErrorCode = "NET_TIMEOUT"
            };

            // Act
            var result = await orchestrator.AttemptRecoveryAsync(error);

            // Assert
            Assert.True(result);
            _analyticsMock.Verify(x => x.TrackRecoverySuccessAsync(It.IsAny<ErrorRecoveryContext>()), Times.Once);
        }

        [Fact]
        public void ErrorRecoveryOrchestrator_ShouldMaintainSeparateCircuitBreakers()
        {
            // Arrange
            var networkStrategy = new NetworkTimeoutRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            var resourceStrategy = new ResourceExhaustionRecoveryStrategy(_retryPolicy, _analyticsMock.Object);
            
            var orchestrator = new ErrorRecoveryOrchestrator(
                new[] { networkStrategy, resourceStrategy },
                _analyticsMock.Object,
                _retryPolicy);

            // Act
            var resourceState = orchestrator.GetCircuitState(ErrorCategory.Resource);
            var systemState = orchestrator.GetCircuitState(ErrorCategory.System);

            // Assert
            Assert.Equal(CircuitState.Closed, resourceState);
            Assert.Equal(CircuitState.Closed, systemState);
        }
    }
}