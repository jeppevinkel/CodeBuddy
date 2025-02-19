using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.ErrorHandling;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Models.Errors;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.ErrorHandling
{
    public class ConfigurationRecoveryStrategyTests
    {
        private readonly Mock<IConfigurationManager> _configManagerMock;
        private readonly Mock<IConfigurationMigrationManager> _migrationManagerMock;
        private readonly Mock<IErrorAnalyticsService> _analyticsMock;
        private readonly RetryPolicy _retryPolicy;
        private readonly ConfigurationRecoveryStrategy _strategy;

        public ConfigurationRecoveryStrategyTests()
        {
            _configManagerMock = new Mock<IConfigurationManager>();
            _migrationManagerMock = new Mock<IConfigurationMigrationManager>();
            _analyticsMock = new Mock<IErrorAnalyticsService>();
            _retryPolicy = new RetryPolicy 
            { 
                BaseDelayMs = 100,
                MaxRetryAttempts = new() { { ErrorCategory.Configuration, 3 } }
            };

            _strategy = new ConfigurationRecoveryStrategy(
                _retryPolicy,
                _analyticsMock.Object,
                _configManagerMock.Object,
                _migrationManagerMock.Object);
        }

        [Fact]
        public void CanHandle_ConfigurationError_ReturnsTrue()
        {
            // Arrange
            var error = new ValidationError
            {
                Category = ErrorCategory.Configuration,
                Source = ErrorSource.Plugin
            };

            // Act
            var result = _strategy.CanHandle(error);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_NonConfigurationError_ReturnsFalse()
        {
            // Arrange
            var error = new ValidationError
            {
                Category = ErrorCategory.Resource,
                Source = ErrorSource.Plugin
            };

            // Act
            var result = _strategy.CanHandle(error);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AttemptRecoveryAsync_NoValidBackup_ReturnsFalse()
        {
            // Arrange
            var error = new ValidationError
            {
                Category = ErrorCategory.Configuration,
                Source = ErrorSource.Plugin,
                Metadata = new() { { "PluginId", "test-plugin" } }
            };
            var context = ErrorRecoveryContext.Create(error);

            _configManagerMock.Setup(m => m.LoadPluginConfigurationAsync(
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((object)null);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(context);

            // Assert
            Assert.False(result);
            _analyticsMock.Verify(a => a.TrackEventAsync(
                "ConfigurationRecovery_NoValidBackup",
                It.Is<System.Collections.Generic.Dictionary<string, string>>(
                    d => d["PluginId"] == "test-plugin")),
                Times.Once);
        }

        [Fact]
        public async Task AttemptRecoveryAsync_ValidBackup_ReturnsTrue()
        {
            // Arrange
            var error = new ValidationError
            {
                Category = ErrorCategory.Configuration,
                Source = ErrorSource.Plugin,
                Metadata = new() { { "PluginId", "test-plugin" } }
            };
            var context = ErrorRecoveryContext.Create(error);
            var config = new TestPluginConfiguration { Name = "Test" };

            _configManagerMock.Setup(m => m.LoadPluginConfigurationAsync(
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(config);
            
            _configManagerMock.Setup(m => m.ValidateConfigurationAsync(It.IsAny<object>()))
                .ReturnsAsync(true);

            _migrationManagerMock.Setup(m => m.MigrateConfigurationAsync(It.IsAny<object>()))
                .ReturnsAsync(config);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(context);

            // Assert
            Assert.True(result);
            _configManagerMock.Verify(m => m.SavePluginConfigurationAsync(
                "test-plugin", config), Times.Once);
            _analyticsMock.Verify(a => a.TrackEventAsync(
                "ConfigurationRecovery_Attempt",
                It.Is<System.Collections.Generic.Dictionary<string, string>>(
                    d => d["PluginId"] == "test-plugin" && d["Success"] == "True")),
                Times.Once);
        }

        [Fact]
        public async Task BackupConfigurationAsync_Success_CreatesBackup()
        {
            // Arrange
            var pluginId = "test-plugin";
            var config = new TestPluginConfiguration { Name = "Test" };

            _configManagerMock.Setup(m => m.GetPluginConfigurationAsync(pluginId))
                .ReturnsAsync(config);

            // Act
            await _strategy.BackupConfigurationAsync(pluginId);

            // Assert
            _configManagerMock.Verify(m => m.SavePluginConfigurationAsync(
                pluginId, config, It.Is<string>(s => s.Contains(pluginId))), 
                Times.Once);
            _analyticsMock.Verify(a => a.TrackEventAsync(
                "ConfigurationBackup_Created",
                It.Is<System.Collections.Generic.Dictionary<string, string>>(
                    d => d["PluginId"] == pluginId)),
                Times.Once);
        }

        private class TestPluginConfiguration : BaseConfiguration
        {
            public string Name { get; set; }
        }
    }
}