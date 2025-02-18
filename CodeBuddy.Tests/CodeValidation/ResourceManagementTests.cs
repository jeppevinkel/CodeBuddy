using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ResourceManagementTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly TestCodeValidator _validator;

        public ResourceManagementTests()
        {
            _loggerMock = new Mock<ILogger>();
            _validator = new TestCodeValidator(_loggerMock.Object);
        }

        [Fact]
        public async Task ValidateAsync_WithLargeInput_UsesQueueing()
        {
            // Arrange
            var largeCode = new string('x', 2 * 1024 * 1024); // 2MB of data
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var tasks = new List<Task<ValidationResult>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_validator.ValidateAsync(largeCode, "test", options));
            }

            // Assert
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.IsValid));
            Assert.True(_validator.QueueWasUsed);
        }

        [Fact]
        public async Task ValidateAsync_UnderMemoryPressure_PerformsCleanup()
        {
            // Arrange
            var code = new string('x', 1024 * 1024); // 1MB
            var options = new ValidationOptions { ValidateSyntax = true };
            
            // Simulate memory pressure
            GC.AddMemoryPressure(1000 * 1024 * 1024); // Add 1GB pressure

            try
            {
                // Act
                var result = await _validator.ValidateAsync(code, "test", options);

                // Assert
                Assert.True(result.IsValid);
                Assert.True(_validator.EmergencyCleanupPerformed);
            }
            finally
            {
                GC.RemoveMemoryPressure(1000 * 1024 * 1024);
            }
        }

        [Fact]
        public async Task ValidateAsync_WithCancellation_CleansUpResources()
        {
            // Arrange
            var code = new string('x', 1024 * 1024);
            var options = new ValidationOptions { ValidateSyntax = true };
            var cts = new CancellationTokenSource();

            // Act
            var task = _validator.ValidateAsync(code, "test", options, cts.Token);
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.True(_validator.ResourcesCleanedUp);
        }

        [Fact]
        public async Task ResourceMonitoring_TracksUsagePatterns()
        {
            // Arrange
            var code = new string('x', 512 * 1024); // 512KB
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var result = await _validator.ValidateAsync(code, "test", options);

            // Assert
            Assert.NotNull(result.Statistics.Performance);
            Assert.True(result.Statistics.Performance.MemoryUsagePattern.Count > 0);
            Assert.True(result.Statistics.Performance.CpuUtilizationPattern.Count > 0);
        }

        [Fact]
        public async Task ResourcePooling_ReusesObjects()
        {
            // Arrange
            var code = "test code";
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var results = new List<ValidationResult>();
            for (int i = 0; i < 10; i++)
            {
                results.Add(await _validator.ValidateAsync(code, "test", options));
            }

            // Assert
            Assert.True(_validator.ObjectsWereReused);
            Assert.All(results, r => Assert.True(r.IsValid));
        }

        [Fact]
        public async Task EmergencyCleanup_HandlesResourceExhaustion()
        {
            // Arrange
            var code = new string('x', 1024 * 1024); // 1MB
            var options = new ValidationOptions { ValidateSyntax = true };
            _validator.SimulateResourceExhaustion = true;

            // Act
            var result = await _validator.ValidateAsync(code, "test", options);

            // Assert
            Assert.True(result.IsValid);
            Assert.True(_validator.EmergencyCleanupPerformed);
            Assert.Contains(_loggerMock.Invocations, 
                i => i.Arguments.Any(a => a?.ToString()?.Contains("Emergency cleanup") == true));
        }

        private class TestCodeValidator : BaseCodeValidator
        {
            public bool QueueWasUsed { get; private set; }
            public bool EmergencyCleanupPerformed { get; private set; }
            public bool ResourcesCleanedUp { get; private set; }
            public bool ObjectsWereReused { get; private set; }
            public bool SimulateResourceExhaustion { get; set; }

            public TestCodeValidator(ILogger logger) : base(logger)
            {
            }

            protected override Task ValidateSyntaxAsync(string code, ValidationResult result)
            {
                if (SimulateResourceExhaustion)
                {
                    GC.AddMemoryPressure(1000 * 1024 * 1024);
                    EmergencyCleanupPerformed = true;
                    GC.RemoveMemoryPressure(1000 * 1024 * 1024);
                }
                
                result.IsValid = true;
                return Task.CompletedTask;
            }

            protected override Task ValidateSecurityAsync(string code, ValidationResult result)
                => Task.CompletedTask;

            protected override Task ValidateStyleAsync(string code, ValidationResult result)
                => Task.CompletedTask;

            protected override Task ValidateBestPracticesAsync(string code, ValidationResult result)
                => Task.CompletedTask;

            protected override Task ValidateErrorHandlingAsync(string code, ValidationResult result)
                => Task.CompletedTask;

            protected override Task ValidateCustomRulesAsync(string code, ValidationResult result, Dictionary<string, object> customRules)
                => Task.CompletedTask;
        }
    }
}