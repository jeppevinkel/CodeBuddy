using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Models.Analytics;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ErrorRecoveryAnalyticsTests
    {
        private readonly ErrorRecoveryAnalytics _analytics;

        public ErrorRecoveryAnalyticsTests()
        {
            _analytics = new ErrorRecoveryAnalytics();
        }

        [Fact]
        public async Task RecordRecoveryAttempt_UpdatesMetricsCorrectly()
        {
            // Arrange
            var errorType = "TestError";
            var duration = TimeSpan.FromMilliseconds(100);
            var resourceMetrics = new Dictionary<string, double>
            {
                { "CPU", 0.5 },
                { "Memory", 256.0 }
            };

            // Act
            await _analytics.RecordRecoveryAttempt(errorType, true, duration, resourceMetrics);
            var metrics = await _analytics.GetErrorMetrics(errorType);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(1, metrics.AttemptCount);
            Assert.Equal(1, metrics.SuccessCount);
            Assert.Equal(0, metrics.FailureCount);
            Assert.Equal(duration.TotalMilliseconds, metrics.AverageRecoveryTime);
            Assert.Equal(resourceMetrics["CPU"], metrics.ResourceConsumption["CPU"]);
            Assert.Equal(resourceMetrics["Memory"], metrics.ResourceConsumption["Memory"]);
        }

        [Fact]
        public async Task AnalyzeErrorPatterns_ReturnsRelevantPatterns()
        {
            // Arrange
            var errorTypes = new[] { "Error1", "Error2", "Error3" };
            foreach (var error in errorTypes)
            {
                await _analytics.RecordRecoveryAttempt(error, true, TimeSpan.FromMilliseconds(100),
                    new Dictionary<string, double> { { "CPU", 0.5 }, { "Memory", 256.0 } });
            }

            // Act
            var patterns = await _analytics.AnalyzeErrorPatterns();

            // Assert
            Assert.NotEmpty(patterns);
            Assert.All(patterns, pattern =>
            {
                Assert.NotNull(pattern.PatternId);
                Assert.NotEmpty(pattern.Description);
                Assert.NotEmpty(pattern.RelatedErrorTypes);
                Assert.True(pattern.OccurrenceCount > 0);
                Assert.True(pattern.PredictedProbability >= 0 && pattern.PredictedProbability <= 1);
                Assert.NotEmpty(pattern.SuggestedPreventiveMeasures);
            });
        }

        [Fact]
        public async Task CircuitBreakerMetrics_TracksStateTransitions()
        {
            // Arrange
            var service = "TestService";
            var states = new[] { "Closed", "Open", "HalfOpen", "Closed" };

            // Act
            foreach (var state in states)
            {
                await _analytics.UpdateCircuitBreakerMetrics(service, state, $"Transition to {state}");
            }
            var metrics = await _analytics.GetCircuitBreakerStatus(service);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(service, metrics.ServiceName);
            Assert.Equal("Closed", metrics.State);
            Assert.Equal(states.Length, metrics.StateTransitions.Count);
            Assert.All(metrics.StateTransitions, transition =>
            {
                Assert.NotNull(transition.FromState);
                Assert.NotNull(transition.ToState);
                Assert.NotNull(transition.Reason);
                Assert.True(transition.TransitionTime <= DateTime.UtcNow);
            });
        }

        [Fact]
        public async Task EvaluateRecoveryStrategy_CalculatesEfficiencyScore()
        {
            // Arrange
            var strategy = "RetryStrategy";
            var errorType = "NetworkError";
            
            // Simulate success and failure attempts
            for (int i = 0; i < 5; i++)
            {
                await _analytics.RecordRecoveryAttempt(errorType, i % 2 == 0,
                    TimeSpan.FromMilliseconds(100 + i * 50),
                    new Dictionary<string, double>
                    {
                        { "CPU", 0.2 + i * 0.1 },
                        { "Memory", 100.0 + i * 50.0 }
                    });
            }

            // Act
            var score = await _analytics.EvaluateRecoveryStrategy(strategy);

            // Assert
            Assert.NotNull(score);
            Assert.Equal(strategy, score.StrategyName);
            Assert.True(score.SuccessRate >= 0 && score.SuccessRate <= 1);
            Assert.True(score.ResourceCost >= 0);
            Assert.True(score.PerformanceImpact >= 0);
            Assert.True(score.OverallEfficiencyScore >= 0);
        }
    }
}