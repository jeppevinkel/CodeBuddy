using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class AsyncResourceTrackerTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly AsyncResourceTracker _tracker;

        public AsyncResourceTrackerTests()
        {
            _mockLogger = new Mock<ILogger>();
            _tracker = new AsyncResourceTracker(
                _mockLogger.Object,
                maxConcurrentOperations: 2,
                operationTimeout: TimeSpan.FromSeconds(1),
                monitoringInterval: TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public async Task TrackOperationAsync_CompletesSuccessfully()
        {
            // Arrange
            var result = await _tracker.TrackOperationAsync(
                async ct =>
                {
                    await Task.Delay(100, ct);
                    return "success";
                },
                "TestOperation");

            // Assert
            Assert.Equal("success", result);
            Assert.Empty(_tracker.ActiveOperations);
        }

        [Fact]
        public async Task TrackOperationAsync_HandlesTimeout()
        {
            // Arrange
            var tracker = new AsyncResourceTracker(
                _mockLogger.Object,
                operationTimeout: TimeSpan.FromMilliseconds(100));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await tracker.TrackOperationAsync(
                    async ct =>
                    {
                        await Task.Delay(1000, ct);
                        return "success";
                    },
                    "TimeoutOperation");
            });
        }

        [Fact]
        public async Task TrackOperationAsync_EnforcesMaxConcurrentOperations()
        {
            // Arrange
            var operations = new List<Task<string>>();
            var maxOperations = 2;
            var tracker = new AsyncResourceTracker(
                _mockLogger.Object,
                maxConcurrentOperations: maxOperations);

            // Act
            for (int i = 0; i < 5; i++)
            {
                operations.Add(tracker.TrackOperationAsync(
                    async ct =>
                    {
                        await Task.Delay(100, ct);
                        return $"operation_{i}";
                    },
                    $"Operation_{i}"));
            }

            // Assert
            await Task.Delay(50); // Allow some operations to start
            Assert.True(tracker.CurrentOperationCount <= maxOperations);
            
            await Task.WhenAll(operations);
        }

        [Fact]
        public async Task TrackOperationAsync_HandlesOperationFailure()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _tracker.TrackOperationAsync<string>(
                    async ct =>
                    {
                        await Task.Delay(10, ct);
                        throw new InvalidOperationException("Test failure");
                    },
                    "FailingOperation");
            });

            Assert.Empty(_tracker.ActiveOperations);
        }

        [Fact]
        public async Task TrackOperationAsync_DetectsStaleOperations()
        {
            // Arrange
            var staleTask = _tracker.TrackOperationAsync(
                async ct =>
                {
                    try
                    {
                        await Task.Delay(2000, ct);
                        return "success";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                },
                "StaleOperation");

            // Wait for operation to become stale
            await Task.Delay(1500);

            // Assert
            var staleOps = _tracker.ActiveOperations
                .Where(op => op.Status == AsyncResourceTracker.OperationStatus.Cancelled)
                .ToList();

            Assert.NotEmpty(staleOps);
            await Assert.ThrowsAsync<OperationCanceledException>(() => staleTask);
        }

        [Fact]
        public async Task Dispose_CancelsActiveOperations()
        {
            // Arrange
            var operation = _tracker.TrackOperationAsync(
                async ct =>
                {
                    await Task.Delay(1000, ct);
                    return "success";
                },
                "LongRunningOperation");

            // Act
            await _tracker.DisposeAsync();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
            Assert.Empty(_tracker.ActiveOperations);
        }
    }
}