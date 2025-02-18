using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.ResourceManagement;
using CodeBuddy.Core.Models.ValidationModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ResourcePreallocationTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<ResourceUsageTracker> _resourceTrackerMock;
        private readonly ResourceTrendAnalyzer _trendAnalyzer;
        private readonly Mock<ValidationPipelineDashboard> _dashboardMock;
        private readonly ResourcePreallocationManager _manager;

        public ResourcePreallocationTests()
        {
            _loggerMock = new Mock<ILogger>();
            _resourceTrackerMock = new Mock<ResourceUsageTracker>();
            _trendAnalyzer = new ResourceTrendAnalyzer();
            _dashboardMock = new Mock<ValidationPipelineDashboard>();

            _manager = new ResourcePreallocationManager(
                _resourceTrackerMock.Object,
                _trendAnalyzer,
                _dashboardMock.Object);
        }

        [Fact]
        public async Task PreallocateResources_WithHighPriority_AllocatesExtraResources()
        {
            // Arrange
            var context = new ValidationContext
            {
                CodeSize = 10000,
                ValidationType = new ValidationType(new[] { ValidationSubType.Syntax }),
                IsHighPriority = true,
                EstimatedComplexity = 75
            };

            // Act
            var allocation = await _manager.PreallocateResourcesAsync(context);

            // Assert
            Assert.NotNull(allocation);
            Assert.Equal(ValidationPriority.High, allocation.Priority);
            Assert.NotNull(allocation.MemoryPool);
            Assert.NotNull(allocation.FileHandles);
        }

        [Fact]
        public async Task PreallocateResources_WithLargeCodeSize_AllocatesProportionalMemory()
        {
            // Arrange
            var context = new ValidationContext
            {
                CodeSize = 1000000, // 1MB
                ValidationType = new ValidationType(new[] { ValidationSubType.Syntax }),
                IsHighPriority = false,
                EstimatedComplexity = 50
            };

            // Act
            var allocation = await _manager.PreallocateResourcesAsync(context);

            // Assert
            Assert.NotNull(allocation);
            Assert.NotNull(allocation.MemoryPool);
            Assert.True(allocation.MemoryPool.Size >= context.CodeSize * 2);
        }

        [Fact]
        public async Task PreallocateResources_WithMultipleValidationTypes_AllocatesAdequateResources()
        {
            // Arrange
            var context = new ValidationContext
            {
                CodeSize = 50000,
                ValidationType = new ValidationType(new[] 
                { 
                    ValidationSubType.Syntax,
                    ValidationSubType.Security,
                    ValidationSubType.Style
                }),
                IsHighPriority = false,
                EstimatedComplexity = 30
            };

            // Act
            var allocation = await _manager.PreallocateResourcesAsync(context);

            // Assert
            Assert.NotNull(allocation);
            Assert.NotNull(allocation.MemoryPool);
            Assert.NotNull(allocation.FileHandles);
            Assert.True(allocation.FileHandles.Size >= 3); // At least one handle per validation type
        }

        [Fact]
        public async Task ResourceTrendAnalyzer_PredictionsImprove_WithMoreHistory()
        {
            // Arrange
            var context = new ValidationContext
            {
                CodeSize = 20000,
                ValidationType = new ValidationType(new[] { ValidationSubType.Syntax }),
                IsHighPriority = false,
                EstimatedComplexity = 25
            };

            // Record some usage history
            for (int i = 0; i < 15; i++)
            {
                _trendAnalyzer.RecordUsage(context, new ResourceUsageStats
                {
                    MemoryUsed = 50000,
                    FileHandlesUsed = 5,
                    Duration = TimeSpan.FromMilliseconds(100)
                });
                await Task.Delay(10); // Small delay between records
            }

            // Act
            var prediction = await _trendAnalyzer.PredictResourceNeeds(context);

            // Assert
            Assert.True(prediction.ConfidenceScore > 0.7);
            Assert.True(prediction.EstimatedMemoryNeeded >= context.CodeSize);
            Assert.True(prediction.EstimatedFileHandles >= 3);
        }

        [Fact]
        public async Task ResourcePreallocation_WithHighLoad_ScalesResourcePools()
        {
            // Arrange
            var context = new ValidationContext
            {
                CodeSize = 30000,
                ValidationType = new ValidationType(new[] { ValidationSubType.Syntax }),
                IsHighPriority = true,
                EstimatedComplexity = 40
            };

            // Simulate high load
            for (int i = 0; i < 20; i++)
            {
                await _manager.PreallocateResourcesAsync(context);
            }

            // Act
            await _manager.OptimizeResourcePoolsAsync();

            // Assert
            _dashboardMock.Verify(d => d.TrackAllocation(It.IsAny<ResourceAllocation>()), Times.AtLeast(20));
        }
    }
}