using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Analytics;
using CodeBuddy.Core.Models;
using NUnit.Framework;

namespace CodeBuddy.Tests.CodeValidation
{
    [TestFixture]
    public class TimeSeriesStorageTests
    {
        private string _testStoragePath;
        private ITimeSeriesStorageOptions _options;
        private ITimeSeriesStorage _storage;

        [SetUp]
        public void Setup()
        {
            _testStoragePath = Path.Combine(Path.GetTempPath(), $"timeseries_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testStoragePath);

            _options = new TimeSeriesStorageOptions
            {
                StoragePath = _testStoragePath,
                RetentionPeriod = TimeSpan.FromDays(30),
                SamplingRate = TimeSpan.FromSeconds(1),
                EnableCompression = true,
                MaxDataPointsInMemory = 1000
            };

            _storage = new TimeSeriesStorage(_options);
        }

        [TearDown]
        public void TearDown()
        {
            (_storage as IDisposable)?.Dispose();
            if (Directory.Exists(_testStoragePath))
            {
                Directory.Delete(_testStoragePath, true);
            }
        }

        [Test]
        public async Task StoreDataPoint_ShouldPersistData()
        {
            // Arrange
            var dataPoint = new TimeSeriesDataPoint
            {
                Timestamp = DateTime.UtcNow,
                Metrics = new Dictionary<string, double> { { "cpu", 75.5 } },
                Tags = new Dictionary<string, string> { { "host", "server1" } }
            };

            // Act
            await _storage.StoreDataPointAsync(dataPoint);
            var result = await _storage.GetDataPointsAsync(
                dataPoint.Timestamp.AddMinutes(-1),
                dataPoint.Timestamp.AddMinutes(1));

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            var storedPoint = result.First();
            Assert.That(storedPoint.Timestamp, Is.EqualTo(dataPoint.Timestamp));
            Assert.That(storedPoint.Metrics["cpu"], Is.EqualTo(75.5));
            Assert.That(storedPoint.Tags["host"], Is.EqualTo("server1"));
        }

        [Test]
        public async Task GetDataPoints_WithTags_ShouldFilterCorrectly()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var points = new[]
            {
                new TimeSeriesDataPoint
                {
                    Timestamp = now,
                    Metrics = new Dictionary<string, double> { { "cpu", 75.5 } },
                    Tags = new Dictionary<string, string> { { "host", "server1" } }
                },
                new TimeSeriesDataPoint
                {
                    Timestamp = now.AddSeconds(1),
                    Metrics = new Dictionary<string, double> { { "cpu", 80.0 } },
                    Tags = new Dictionary<string, string> { { "host", "server2" } }
                }
            };

            foreach (var point in points)
            {
                await _storage.StoreDataPointAsync(point);
            }

            // Act
            var result = await _storage.GetDataPointsAsync(
                now.AddSeconds(-1),
                now.AddSeconds(2),
                new Dictionary<string, string> { { "host", "server1" } });

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            var storedPoint = result.First();
            Assert.That(storedPoint.Tags["host"], Is.EqualTo("server1"));
        }

        [Test]
        public async Task PruneData_ShouldRemoveOldData()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var oldPoint = new TimeSeriesDataPoint
            {
                Timestamp = now.AddDays(-31),
                Metrics = new Dictionary<string, double> { { "cpu", 75.5 } },
                Tags = new Dictionary<string, string> { { "host", "server1" } }
            };
            var newPoint = new TimeSeriesDataPoint
            {
                Timestamp = now,
                Metrics = new Dictionary<string, double> { { "cpu", 80.0 } },
                Tags = new Dictionary<string, string> { { "host", "server1" } }
            };

            await _storage.StoreDataPointAsync(oldPoint);
            await _storage.StoreDataPointAsync(newPoint);

            // Act
            await _storage.PruneDataAsync(now.AddDays(-30));
            var result = await _storage.GetDataPointsAsync(
                now.AddDays(-32),
                now.AddDays(1));

            // Assert
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.First().Timestamp, Is.EqualTo(newPoint.Timestamp));
        }

        [Test]
        public async Task SamplingRate_ShouldLimitDataPoints()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var points = Enumerable.Range(0, 10).Select(i => new TimeSeriesDataPoint
            {
                Timestamp = now.AddMilliseconds(i * 100),
                Metrics = new Dictionary<string, double> { { "cpu", 75.5 + i } },
                Tags = new Dictionary<string, string> { { "host", "server1" } }
            });

            // Act
            foreach (var point in points)
            {
                await _storage.StoreDataPointAsync(point);
            }

            var result = await _storage.GetDataPointsAsync(
                now.AddSeconds(-1),
                now.AddSeconds(2));

            // Assert
            Assert.That(result.Count(), Is.LessThan(10),
                "Sampling rate should prevent storing all high-frequency data points");
        }

        [Test]
        public async Task DataCompression_ShouldReduceStorageSize()
        {
            // Arrange
            var compressedOptions = new TimeSeriesStorageOptions
            {
                StoragePath = Path.Combine(_testStoragePath, "compressed"),
                EnableCompression = true
            };
            var uncompressedOptions = new TimeSeriesStorageOptions
            {
                StoragePath = Path.Combine(_testStoragePath, "uncompressed"),
                EnableCompression = false
            };

            Directory.CreateDirectory(compressedOptions.StoragePath);
            Directory.CreateDirectory(uncompressedOptions.StoragePath);

            using var compressedStorage = new TimeSeriesStorage(compressedOptions);
            using var uncompressedStorage = new TimeSeriesStorage(uncompressedOptions);

            var now = DateTime.UtcNow;
            var points = Enumerable.Range(0, 1000).Select(i => new TimeSeriesDataPoint
            {
                Timestamp = now.AddSeconds(i),
                Metrics = new Dictionary<string, double> { { "cpu", 75.5 + i } },
                Tags = new Dictionary<string, string> { { "host", "server1" } }
            });

            // Act
            foreach (var point in points)
            {
                await compressedStorage.StoreDataPointAsync(point);
                await uncompressedStorage.StoreDataPointAsync(point);
            }

            // Force flush to disk
            (compressedStorage as IDisposable).Dispose();
            (uncompressedStorage as IDisposable).Dispose();

            var compressedSize = Directory.GetFiles(compressedOptions.StoragePath)
                .Sum(f => new FileInfo(f).Length);
            var uncompressedSize = Directory.GetFiles(uncompressedOptions.StoragePath)
                .Sum(f => new FileInfo(f).Length);

            // Assert
            Assert.That(compressedSize, Is.LessThan(uncompressedSize),
                "Compressed storage should use less disk space");
        }
    }
}