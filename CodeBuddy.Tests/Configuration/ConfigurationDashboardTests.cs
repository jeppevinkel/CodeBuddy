using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Moq;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Implementation.Configuration;
using CodeBuddy.Core.Interfaces;

namespace CodeBuddy.Tests.Configuration
{
    public class ConfigurationDashboardTests
    {
        private readonly Mock<IConfigurationManager> _configManagerMock;
        private readonly Mock<IConfigurationValidator> _validatorMock;
        private readonly Mock<IConfigurationMigrationManager> _migrationManagerMock;
        private readonly Mock<ILoggingService> _loggerMock;
        private readonly ConfigurationDashboard _dashboard;

        public ConfigurationDashboardTests()
        {
            _configManagerMock = new Mock<IConfigurationManager>();
            _validatorMock = new Mock<IConfigurationValidator>();
            _migrationManagerMock = new Mock<IConfigurationMigrationManager>();
            _loggerMock = new Mock<ILoggingService>();

            _dashboard = new ConfigurationDashboard(
                _configManagerMock.Object,
                _validatorMock.Object,
                _migrationManagerMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetConfigurationHealthStatus_ReturnsValidStatus()
        {
            // Arrange
            var components = new List<string> { "Component1", "Component2" };
            _configManagerMock.Setup(x => x.GetAllComponentsAsync())
                .ReturnsAsync(components);

            var config = new BaseConfiguration { Environment = "Test", Version = "1.0" };
            _configManagerMock.Setup(x => x.GetConfigurationAsync(It.IsAny<string>()))
                .ReturnsAsync(config);

            var validationResult = new ValidationResult { IsValid = true, Issues = new List<ConfigurationValidationIssue>() };
            _validatorMock.Setup(x => x.ValidateConfigurationAsync(It.IsAny<BaseConfiguration>()))
                .ReturnsAsync(validationResult);

            // Act
            var result = await _dashboard.GetConfigurationHealthStatusAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, status =>
            {
                Assert.True(status.IsHealthy);
                Assert.Equal("Test", status.Environment);
                Assert.Equal("1.0", status.Version);
            });
        }

        [Fact]
        public async Task GetPerformanceMetrics_ReturnsValidMetrics()
        {
            // Arrange
            _configManagerMock.Setup(x => x.GetAverageLoadTimeAsync()).ReturnsAsync(100.0);
            _validatorMock.Setup(x => x.GetAverageValidationTimeAsync()).ReturnsAsync(50.0);
            _configManagerMock.Setup(x => x.GetCacheHitRateAsync()).ReturnsAsync(85);

            // Act
            var result = await _dashboard.GetPerformanceMetricsAsync();

            // Assert
            Assert.Equal(100.0, result.LoadTimeMs);
            Assert.Equal(50.0, result.ValidationTimeMs);
            Assert.Equal(85, result.CacheHitRate);
            Assert.True(result.MeasuredAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task TrackConfigurationAccess_LogsAccess()
        {
            // Arrange
            var section = "TestSection";

            // Act
            await _dashboard.TrackConfigurationAccessAsync(section);

            // Assert
            _configManagerMock.Verify(x => x.TrackConfigurationAccessAsync(section), Times.Once);
        }

        [Fact]
        public async Task AlertOnValidationFailure_LogsWarning()
        {
            // Arrange
            var issue = new ConfigurationValidationIssue
            {
                IssueType = "Error",
                Message = "Test Error",
                Section = "TestSection",
                DetectedAt = DateTime.UtcNow
            };

            // Act
            await _dashboard.AlertOnValidationFailureAsync(issue);

            // Assert
            _loggerMock.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
        }
    }
}