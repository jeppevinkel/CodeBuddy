using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models.Analytics;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.ErrorHandling
{
    public class PreemptiveErrorHandlerTests
    {
        private readonly Mock<ITimeSeriesStorage> _timeSeriesStorageMock;
        private readonly Mock<IErrorAnalyticsService> _analyticsServiceMock;
        private readonly PreemptiveErrorHandler _handler;

        public PreemptiveErrorHandlerTests()
        {
            _timeSeriesStorageMock = new Mock<ITimeSeriesStorage>();
            _analyticsServiceMock = new Mock<IErrorAnalyticsService>();
            _handler = new PreemptiveErrorHandler(_timeSeriesStorageMock.Object, _analyticsServiceMock.Object);
        }

        [Fact]
        public async Task PredictPotentialErrors_WithHighMemoryGrowth_PredictsMemoryLeak()
        {
            // Arrange
            var metrics = new List<TimeSeriesDataPoint>
            {
                CreateMemoryMetric(DateTime.UtcNow.AddMinutes(-30), 1000),
                CreateMemoryMetric(DateTime.UtcNow, 2000)
            };

            _timeSeriesStorageMock.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
                .ReturnsAsync(metrics);

            // Act
            var predictions = await _handler.PredictPotentialErrors();

            // Assert
            Assert.Contains(predictions, p => p.ErrorType == "MemoryLeak");
            var memoryLeak = predictions.Find(p => p.ErrorType == "MemoryLeak");
            Assert.True(memoryLeak.ProbabilityScore > 0.5);
            Assert.Contains("ForcedGC", memoryLeak.RecommendedActions);
        }

        [Fact]
        public async Task AnalyzeRiskFactors_WithHighResourceUtilization_IdentifiesRisk()
        {
            // Arrange
            var metrics = new List<TimeSeriesDataPoint>
            {
                CreateResourceMetric(DateTime.UtcNow, 0.95)
            };

            _timeSeriesStorageMock.Setup(x => x.GetDataPointsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
                .ReturnsAsync(metrics);

            // Act
            var riskFactors = await _handler.AnalyzeRiskFactors();

            // Assert
            Assert.True(riskFactors.ContainsKey("resource_utilization"));
            Assert.True(riskFactors["resource_utilization"] > 0.9);
        }

        [Fact]
        public async Task TriggerPreventiveAction_WithValidAction_ExecutesSuccessfully()
        {
            // Act
            var result = await _handler.TriggerPreventiveAction("MemoryLeak", "ForcedGC");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task UpdatePredictionModel_UpdatesBasedOnHistoricalData()
        {
            // Arrange
            var window = TimeSpan.FromDays(7);
            var patterns = new List<ErrorFrequencyPattern>
            {
                new ErrorFrequencyPattern
                {
                    StartTime = DateTime.UtcNow.AddDays(-1),
                    EndTime = DateTime.UtcNow,
                    Frequency = 10,
                    ErrorType = "MemoryLeak"
                }
            };

            _analyticsServiceMock.Setup(x => x.AnalyzeErrorPatterns(It.IsAny<DateTime>()))
                .ReturnsAsync(patterns);

            // Act & Assert
            await _handler.UpdatePredictionModel(window);
        }

        private TimeSeriesDataPoint CreateMemoryMetric(DateTime timestamp, double value)
        {
            return new TimeSeriesDataPoint
            {
                Timestamp = timestamp,
                Metrics = new Dictionary<string, double>
                {
                    { "value", value },
                    { "gc_pressure", 0.9 }
                },
                Tags = new Dictionary<string, string>
                {
                    { "category", "memory" }
                }
            };
        }

        private TimeSeriesDataPoint CreateResourceMetric(DateTime timestamp, double utilization)
        {
            return new TimeSeriesDataPoint
            {
                Timestamp = timestamp,
                Metrics = new Dictionary<string, double>
                {
                    { "utilization", utilization }
                },
                Tags = new Dictionary<string, string>
                {
                    { "category", "resource" }
                }
            };
        }
    }
}