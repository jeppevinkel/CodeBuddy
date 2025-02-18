using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.CodeValidation.Caching;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ValidationCacheTests
    {
        private readonly ValidationCache _cache;
        private readonly ValidationCacheConfig _config;

        public ValidationCacheTests()
        {
            _config = new ValidationCacheConfig
            {
                MaxCacheSizeMB = 100,
                DefaultTTLMinutes = 60,
                MaxEntries = 1000
            };
            var options = new Mock<IOptions<ValidationCacheConfig>>();
            options.Setup(o => o.Value).Returns(_config);
            _cache = new ValidationCache(options.Object);
        }

        [Fact]
        public async Task Cache_StoresAndRetrievesResults()
        {
            // Arrange
            var codeHash = "test-hash";
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync(codeHash, options, result);
            var (found, cachedResult) = await _cache.TryGetAsync(codeHash, options);

            // Assert
            Assert.True(found);
            Assert.NotNull(cachedResult);
            Assert.True(cachedResult.IsValid);
        }

        [Fact]
        public async Task Cache_ExpiresResults()
        {
            // Arrange
            _config.DefaultTTLMinutes = 0; // Immediate expiration
            var codeHash = "test-hash";
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync(codeHash, options, result);
            await Task.Delay(100); // Wait for expiration
            var (found, _) = await _cache.TryGetAsync(codeHash, options);

            // Assert
            Assert.False(found);
        }

        [Fact]
        public async Task Cache_EnforcesMaxEntries()
        {
            // Arrange
            _config.MaxEntries = 2;
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync("hash1", options, result);
            await _cache.SetAsync("hash2", options, result);
            await _cache.SetAsync("hash3", options, result);

            var (found1, _) = await _cache.TryGetAsync("hash1", options);
            var (found2, _) = await _cache.TryGetAsync("hash2", options);
            var (found3, _) = await _cache.TryGetAsync("hash3", options);

            // Assert
            Assert.False(found1); // Should be evicted
            Assert.True(found2);
            Assert.True(found3);
        }

        [Fact]
        public async Task Cache_ClearsProperly()
        {
            // Arrange
            var codeHash = "test-hash";
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync(codeHash, options, result);
            await _cache.InvalidateAsync("test");
            var (found, _) = await _cache.TryGetAsync(codeHash, options);

            // Assert
            Assert.False(found);
        }

        [Fact]
        public async Task Cache_ProvidesAccurateStats()
        {
            // Arrange
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync("hash1", options, result);
            var (found, _) = await _cache.TryGetAsync("hash1", options);
            var (notFound, _) = await _cache.TryGetAsync("hash2", options);
            var stats = await _cache.GetStatsAsync();

            // Assert
            Assert.Equal(1, stats.TotalEntries);
            Assert.Equal(1, stats.CacheHits);
            Assert.Equal(1, stats.CacheMisses);
            Assert.Equal(0.5, stats.HitRatio);
        }

        [Fact]
        public async Task Cache_HandlesMaintenanceProperly()
        {
            // Arrange
            _config.DefaultTTLMinutes = 0; // Immediate expiration
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync("hash1", options, result);
            await Task.Delay(100); // Wait for expiration
            await _cache.MaintenanceAsync();
            var stats = await _cache.GetStatsAsync();

            // Assert
            Assert.Equal(0, stats.TotalEntries); // All entries should be removed
        }

        [Fact]
        public async Task Cache_HandlesInvalidation_WithPattern()
        {
            // Arrange
            var options = new ValidationOptions();
            var result = new ValidationResult { IsValid = true };

            // Act
            await _cache.SetAsync("test1", options, result);
            await _cache.SetAsync("test2", options, result);
            await _cache.SetAsync("other", options, result);
            await _cache.InvalidateAsync("test", "test");

            var (found1, _) = await _cache.TryGetAsync("test1", options);
            var (found2, _) = await _cache.TryGetAsync("test2", options);
            var (found3, _) = await _cache.TryGetAsync("other", options);

            // Assert
            Assert.False(found1);
            Assert.False(found2);
            Assert.True(found3);
        }
    }
}