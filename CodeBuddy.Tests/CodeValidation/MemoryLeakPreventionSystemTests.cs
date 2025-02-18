using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.Memory;
using CodeBuddy.Core.Models;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class MemoryLeakPreventionSystemTests
    {
        private readonly ValidationResilienceConfig _config;
        private readonly MemoryLeakPreventionSystem _preventionSystem;

        public MemoryLeakPreventionSystemTests()
        {
            _config = new ValidationResilienceConfig
            {
                LeakConfidenceThreshold = 90,
                MaxFragmentationPercent = 40.0,
                EnableMemoryLeakAlerts = true,
                MemoryAnalysisInterval = TimeSpan.FromMinutes(10)
            };
            _preventionSystem = new MemoryLeakPreventionSystem(_config);
        }

        [Fact]
        public async Task PredictMemoryLeak_DetectsHighProbabilityLeaks()
        {
            // Arrange
            var context = new ValidationContext
            {
                Code = "var x = new byte[1000000]; // Large allocation",
                Language = "CSharp",
                IsCriticalValidation = true
            };

            // Act
            var result = await _preventionSystem.PredictMemoryLeakAsync(context);

            // Assert
            Assert.True(result);
            Assert.True(context.MemoryProfile.ResourceLeakWarnings > 0);
        }

        [Fact]
        public void TrackResource_ReturnsDisposableTracker()
        {
            // Arrange
            var context = new ValidationContext();
            var resourceId = "test-resource";

            // Act
            using var tracker = _preventionSystem.TrackResource(resourceId, context);

            // Assert
            Assert.NotNull(tracker);
            Assert.True(context.ManagedResources.ContainsKey(resourceId));
        }

        [Fact]
        public void PooledObjects_AreReused()
        {
            // Arrange & Act
            var obj1 = _preventionSystem.GetPooledObject<TestPoolObject>();
            _preventionSystem.ReleasePooledObject(obj1);
            var obj2 = _preventionSystem.GetPooledObject<TestPoolObject>();

            // Assert
            Assert.Same(obj1, obj2);
        }

        [Fact]
        public async Task ManagedContext_TracksMemoryUsage()
        {
            // Arrange & Act
            var context = await _preventionSystem.CreateManagedContextAsync();

            // Assert
            Assert.NotNull(context);
            Assert.NotNull(context.MemoryProfile);
            Assert.Empty(context.MemoryProfile.UsageHistory);
        }

        [Fact]
        public async Task ValidateImplementation_ChecksMemoryPatterns()
        {
            // Arrange
            var validator = new TestValidator();

            // Act & Assert
            await _preventionSystem.ValidateImplementationAsync(validator);
        }

        private class TestPoolObject
        {
            public string Data { get; set; }
        }

        private class TestValidator : ICodeValidator
        {
            public Task<ValidationResult> ValidateAsync(string code, ValidationOptions options)
            {
                return Task.FromResult(new ValidationResult());
            }
        }
    }
}