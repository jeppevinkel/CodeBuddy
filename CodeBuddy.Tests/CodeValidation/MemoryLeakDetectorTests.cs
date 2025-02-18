using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Monitoring;
using CodeBuddy.Core.Models;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class MemoryLeakDetectorTests
    {
        private readonly ValidationResilienceConfig _config;
        private readonly MemoryLeakDetector _detector;

        public MemoryLeakDetectorTests()
        {
            _config = new ValidationResilienceConfig
            {
                MemoryAnalysisInterval = TimeSpan.FromMinutes(10),
                MemoryGrowthThresholdPercent = 5.0,
                LeakConfidenceThreshold = 90,
                EnableAutomaticMemoryDump = true,
                MemorySamplingRate = 60,
                LohGrowthThresholdMB = 100.0,
                MaxFinalizationQueueLength = 1000,
                MaxFragmentationPercent = 40.0
            };

            _detector = new MemoryLeakDetector(_config);
        }

        [Fact]
        public async Task AnalyzeMemoryPatterns_NoLeak_ReturnsLowConfidence()
        {
            // Arrange
            var componentId = "test-component-1";

            // Act
            var result = await _detector.AnalyzeMemoryPatterns(componentId);

            // Assert
            Assert.False(result.LeakDetected);
            Assert.True(result.ConfidenceLevel < _config.LeakConfidenceThreshold);
        }

        [Fact]
        public async Task AnalyzeMemoryPatterns_WithLeak_ReturnsHighConfidence()
        {
            // Arrange
            var componentId = "test-component-2";
            
            // Create artificial memory pressure
            var list = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < 100; i++)
            {
                list.Add(new byte[1024 * 1024]); // Allocate 1MB
                await Task.Delay(10);
            }

            // Act
            var result = await _detector.AnalyzeMemoryPatterns(componentId);

            // Assert
            Assert.True(result.LeakDetected);
            Assert.True(result.ConfidenceLevel >= _config.LeakConfidenceThreshold);
            
            // Cleanup
            list.Clear();
            GC.Collect();
        }

        [Fact]
        public async Task AnalyzeMemoryPatterns_NewComponent_ReturnsNoLeakResult()
        {
            // Arrange
            var componentId = Guid.NewGuid().ToString();

            // Act
            var result = await _detector.AnalyzeMemoryPatterns(componentId);

            // Assert
            Assert.False(result.LeakDetected);
            Assert.Equal(0, result.ConfidenceLevel);
        }

        [Theory]
        [InlineData(1.0, false)]  // Low growth
        [InlineData(10.0, true)]  // High growth
        public async Task AnalyzeMemoryPatterns_DifferentGrowthRates_DetectsLeaksCorrectly(
            double growthThreshold, 
            bool expectedLeakDetection)
        {
            // Arrange
            var customConfig = new ValidationResilienceConfig
            {
                MemoryGrowthThresholdPercent = growthThreshold,
                LeakConfidenceThreshold = 90
            };
            var detector = new MemoryLeakDetector(customConfig);
            var componentId = $"test-component-growth-{growthThreshold}";

            // Create some memory allocations
            var list = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < 50; i++)
            {
                list.Add(new byte[1024 * 1024]); // Allocate 1MB
                await Task.Delay(10);
            }

            // Act
            var result = await detector.AnalyzeMemoryPatterns(componentId);

            // Assert
            Assert.Equal(expectedLeakDetection, result.LeakDetected);
            
            // Cleanup
            list.Clear();
            GC.Collect();
        }
    }
}