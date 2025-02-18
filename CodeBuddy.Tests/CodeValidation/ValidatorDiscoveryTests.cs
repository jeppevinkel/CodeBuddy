using System;
using System.IO;
using CodeBuddy.Core.Implementation.CodeValidation;
using CodeBuddy.Core.Implementation.CodeValidation.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ValidatorDiscoveryTests
    {
        private readonly Mock<ILogger<ValidatorDiscoveryService>> _loggerMock;
        private readonly Mock<IValidatorRegistry> _registryMock;
        private readonly IConfiguration _configuration;
        private readonly string _testValidatorPath;

        public ValidatorDiscoveryTests()
        {
            _loggerMock = new Mock<ILogger<ValidatorDiscoveryService>>();
            _registryMock = new Mock<IValidatorRegistry>();
            
            _testValidatorPath = Path.Combine(Path.GetTempPath(), "TestValidators");
            Directory.CreateDirectory(_testValidatorPath);

            var configData = new Dictionary<string, string>
            {
                {"ValidatorDiscovery:ValidatorPath", _testValidatorPath},
                {"ValidatorDiscovery:DependencyPath", Path.Combine(_testValidatorPath, "Dependencies")},
                {"ValidatorDiscovery:IncludeSubdirectories", "true"},
                {"ValidatorDiscovery:EnableHotReload", "true"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        [Fact]
        public void DiscoverAndRegisterValidators_ShouldLoadValidators()
        {
            // Arrange
            var discoveryService = new ValidatorDiscoveryService(
                _loggerMock.Object,
                _configuration,
                _registryMock.Object);

            // Act
            discoveryService.DiscoverAndRegisterValidators();

            // Assert
            _registryMock.Verify(r => r.Register(
                It.IsAny<ICodeValidator>(),
                It.IsAny<ValidatorMetadataAttribute>(),
                It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public void ValidatorRegistry_ShouldHandleVersionConflicts()
        {
            // Arrange
            var logger = new Mock<ILogger<ValidatorRegistry>>().Object;
            var registry = new ValidatorRegistry(logger);
            var validator1 = new CSharpCodeValidator();
            var validator2 = new CSharpCodeValidator();
            var metadata = new ValidatorMetadataAttribute(
                new[] { "csharp:10.0" },
                performanceProfile: PerformanceProfile.Normal);

            // Act
            registry.Register(validator1, metadata, "1.0.0");
            registry.Register(validator2, metadata, "1.0.0");

            // Assert
            var resolvedValidator = registry.GetValidator("csharp", "10.0");
            Assert.NotNull(resolvedValidator);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_testValidatorPath, true);
            }
            catch { }
        }
    }
}