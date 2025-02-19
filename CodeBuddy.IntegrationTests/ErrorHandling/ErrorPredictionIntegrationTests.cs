using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Linq;

namespace CodeBuddy.IntegrationTests.ErrorHandling
{
    public class ErrorPredictionIntegrationTests
    {
        private readonly IServiceProvider _serviceProvider;

        public ErrorPredictionIntegrationTests()
        {
            var services = new ServiceCollection();
            services.AddCodeBuddyCore();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ErrorPrediction_EndToEnd_Test()
        {
            // Arrange
            var dashboard = _serviceProvider.GetRequiredService<IErrorMonitoringDashboard>();
            var timeSeriesStorage = _serviceProvider.GetRequiredService<ITimeSeriesStorage>();

            // Simulate resource metrics
            await timeSeriesStorage.StoreDataPointAsync(new TimeSeriesDataPoint
            {
                Timestamp = DateTime.UtcNow,
                Metrics = new System.Collections.Generic.Dictionary<string, double>
                {
                    { "utilization", 0.95 },
                    { "growth_rate", 0.25 }
                },
                Tags = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "category", "resource" }
                }
            });

            // Act
            var predictiveMetrics = await dashboard.GetPredictiveMetrics();

            // Assert
            Assert.NotNull(predictiveMetrics);
            Assert.NotEmpty(predictiveMetrics.PredictedErrors);
            Assert.NotEmpty(predictiveMetrics.RiskFactors);

            var resourceExhaustionPrediction = predictiveMetrics.PredictedErrors
                .FirstOrDefault(p => p.ErrorType == "ResourceExhaustion");
            Assert.NotNull(resourceExhaustionPrediction);
            Assert.True(resourceExhaustionPrediction.ProbabilityScore > 0.7);

            // Test preventive action
            var actionResult = await dashboard.ExecutePreventiveAction(
                "ResourceExhaustion",
                "ScaleResources:CPU");
            Assert.True(actionResult);
        }

        [Fact]
        public async Task ErrorPrediction_HistoricalAnalysis_Test()
        {
            // Arrange
            var preemptiveHandler = _serviceProvider.GetRequiredService<IPreemptiveErrorHandler>();
            var timeSeriesStorage = _serviceProvider.GetRequiredService<ITimeSeriesStorage>();

            // Store historical data
            var startTime = DateTime.UtcNow.AddDays(-7);
            for (int i = 0; i < 168; i++) // One week of hourly data
            {
                await timeSeriesStorage.StoreDataPointAsync(new TimeSeriesDataPoint
                {
                    Timestamp = startTime.AddHours(i),
                    Metrics = new System.Collections.Generic.Dictionary<string, double>
                    {
                        { "memory_usage", 1000 + (i * 10) },
                        { "gc_pressure", 0.5 + (i * 0.002) }
                    },
                    Tags = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "category", "memory" }
                    }
                });
            }

            // Act
            await preemptiveHandler.UpdatePredictionModel(TimeSpan.FromDays(7));
            var predictions = await preemptiveHandler.PredictPotentialErrors();

            // Assert
            Assert.NotEmpty(predictions);
            var memoryLeak = predictions.FirstOrDefault(p => p.ErrorType == "MemoryLeak");
            Assert.NotNull(memoryLeak);
            Assert.Contains("ScaleResources:Memory", memoryLeak.RecommendedActions);
        }
    }
}