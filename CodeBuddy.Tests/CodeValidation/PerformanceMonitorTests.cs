using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Models;
using Xunit;
using System.Threading;

namespace CodeBuddy.Tests.CodeValidation
{
    public class PerformanceMonitorTests
    {
        private readonly PerformanceMonitor _monitor;

        public PerformanceMonitorTests()
        {
            _monitor = new PerformanceMonitor();
        }

        [Fact]
        public void StartMeasurement_ShouldInitializeTimerAndMemoryBaseline()
        {
            // Act
            _monitor.StartMeasurement();
            
            // Assert
            var metrics = _monitor.GetMetrics();
            Assert.True(metrics.StartTime > DateTime.MinValue);
            Assert.True(metrics.InitialMemoryUsage > 0);
        }

        [Fact]
        public void StopMeasurement_ShouldCalculateExecutionTimeAndMemoryUsage()
        {
            // Arrange
            _monitor.StartMeasurement();
            Thread.Sleep(100); // Simulate work

            // Act
            _monitor.StopMeasurement();
            var metrics = _monitor.GetMetrics();

            // Assert
            Assert.True(metrics.ExecutionTime >= 100);
            Assert.True(metrics.MemoryUsage >= 0);
        }

        [Fact]
        public void GetMetrics_WithoutStarting_ShouldReturnEmptyMetrics()
        {
            // Act
            var metrics = _monitor.GetMetrics();

            // Assert
            Assert.Equal(0, metrics.ExecutionTime);
            Assert.Equal(0, metrics.MemoryUsage);
        }

        [Fact]
        public void MultipleValidations_ShouldTrackIndependentMetrics()
        {
            // Act
            _monitor.StartMeasurement();
            Thread.Sleep(50);
            _monitor.StopMeasurement();
            var firstMetrics = _monitor.GetMetrics();

            _monitor.StartMeasurement();
            Thread.Sleep(100);
            _monitor.StopMeasurement();
            var secondMetrics = _monitor.GetMetrics();

            // Assert
            Assert.True(firstMetrics.ExecutionTime < secondMetrics.ExecutionTime);
        }

        [Fact]
        public void ConcurrentValidations_ShouldNotInterfere()
        {
            // Arrange
            var monitors = new List<PerformanceMonitor>();
            var tasks = new List<Task<PerformanceMetrics>>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                var monitor = new PerformanceMonitor();
                monitors.Add(monitor);
                tasks.Add(Task.Run(() =>
                {
                    monitor.StartMeasurement();
                    Thread.Sleep(100);
                    monitor.StopMeasurement();
                    return monitor.GetMetrics();
                }));
            }
            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.All(tasks.Select(t => t.Result), metrics =>
            {
                Assert.True(metrics.ExecutionTime >= 100);
                Assert.True(metrics.MemoryUsage >= 0);
            });
        }

        [Fact]
        public void ResourceIntensiveOperation_ShouldShowSignificantMemoryUsage()
        {
            // Arrange
            _monitor.StartMeasurement();
            var initialMetrics = _monitor.GetMetrics();

            // Act - create large array to consume memory
            var largeArray = new byte[1024 * 1024 * 10]; // 10MB
            Array.Fill(largeArray, (byte)1);
            
            _monitor.StopMeasurement();
            var finalMetrics = _monitor.GetMetrics();

            // Assert
            Assert.True(finalMetrics.MemoryUsage > initialMetrics.MemoryUsage);
        }

        [Fact]
        public void MemoryPressure_ShouldBeDetected()
        {
            // Arrange
            var memoryIntensiveMonitor = new PerformanceMonitor();
            memoryIntensiveMonitor.StartMeasurement();

            // Act - create multiple large arrays to consume memory
            var largeArrays = new List<byte[]>();
            for (int i = 0; i < 5; i++)
            {
                largeArrays.Add(new byte[1024 * 1024 * 20]); // 20MB each
            }

            memoryIntensiveMonitor.StopMeasurement();
            var metrics = memoryIntensiveMonitor.GetMetrics();

            // Assert
            Assert.True(metrics.MemoryPressure > 0);
            Assert.True(metrics.MemoryUsage > 50 * 1024 * 1024); // > 50MB
        }
    }
}