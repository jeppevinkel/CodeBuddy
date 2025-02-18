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
    public class ValidationPipelineManagerTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly ValidationPipelineManager _pipelineManager;

        public ValidationPipelineManagerTests()
        {
            _loggerMock = new Mock<ILogger>();
            _pipelineManager = new ValidationPipelineManager(_loggerMock.Object);
        }

        [Fact]
        public async Task SubmitValidationRequest_ProcessesRequestSuccessfully()
        {
            // Arrange
            var code = "public class Test {}";
            var options = new ValidationOptions { ValidateSyntax = true };

            // Act
            var result = await _pipelineManager.SubmitValidationRequestAsync(code, "csharp", options);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task SubmitValidationRequest_HandlesMultipleConcurrentRequests()
        {
            // Arrange
            var requests = Enumerable.Range(0, 10).Select(_ => 
                _pipelineManager.SubmitValidationRequestAsync(
                    "public class Test {}", 
                    "csharp",
                    new ValidationOptions { ValidateSyntax = true }));

            // Act
            var results = await Task.WhenAll(requests);

            // Assert
            Assert.Equal(10, results.Length);
            Assert.All(results, r => Assert.True(r.IsValid));
        }

        [Fact]
        public async Task SubmitValidationRequest_HandlesRequestCancellation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var task = _pipelineManager.SubmitValidationRequestAsync(
                "public class Test {}", 
                "csharp",
                new ValidationOptions { ValidateSyntax = true },
                cts.Token);

            // Act
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        }

        [Fact]
        public async Task SubmitValidationRequest_HandlesResourceExhaustion()
        {
            // Arrange
            var largeCodes = Enumerable.Range(0, 100).Select(_ => 
                new string('x', 1024 * 1024)); // 1MB each

            var tasks = largeCodes.Select(code => 
                _pipelineManager.SubmitValidationRequestAsync(
                    code,
                    "csharp",
                    new ValidationOptions { ValidateSyntax = true }));

            // Act & Assert
            // Some requests should be queued or rejected when resources are exhausted
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                Task.WhenAll(tasks));
        }

        [Fact]
        public async Task ValidationPipeline_HandlesValidatorErrors()
        {
            // Arrange
            var invalidCode = "class {"; // Syntax error

            // Act
            var result = await _pipelineManager.SubmitValidationRequestAsync(
                invalidCode,
                "csharp",
                new ValidationOptions { ValidateSyntax = true });

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Error);
        }

        [Fact]
        public async Task ValidationPipeline_RespectsSystemResources()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var requests = new List<Task<ValidationResult>>();

            // Act
            // Submit requests until we hit resource limits
            while (stopwatch.ElapsedMilliseconds < 5000) // Test for 5 seconds max
            {
                try
                {
                    requests.Add(_pipelineManager.SubmitValidationRequestAsync(
                        "public class Test {}",
                        "csharp",
                        new ValidationOptions { ValidateSyntax = true }));
                }
                catch (InvalidOperationException)
                {
                    break; // Expected when queue is full
                }
            }

            // Assert
            Assert.True(requests.Count > 0);
            var results = await Task.WhenAll(requests);
            Assert.All(results, r => Assert.True(r.IsValid));
        }

        [Fact]
        public async Task Dispose_CleansUpResourcesAndRejectsNewRequests()
        {
            // Arrange
            await using (var manager = new ValidationPipelineManager(_loggerMock.Object))
            {
                // Submit a request before disposal
                var result = await manager.SubmitValidationRequestAsync(
                    "public class Test {}",
                    "csharp",
                    new ValidationOptions { ValidateSyntax = true });
                Assert.True(result.IsValid);
            }

            // Act & Assert
            // After disposal, new requests should be rejected
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                _pipelineManager.SubmitValidationRequestAsync(
                    "public class Test {}",
                    "csharp",
                    new ValidationOptions { ValidateSyntax = true }));
        }
    }
}