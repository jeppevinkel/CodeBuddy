using System;
using System.Linq;
using CodeBuddy.Core.Implementation.CodeValidation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CodeValidation
{
    public class ValidatorRegistrationTests
    {
        private readonly Mock<ILogger<CodeValidatorFactory>> _mockLogger;
        private readonly ValidatorRegistry _registry;
        private readonly CodeValidatorFactory _factory;

        public ValidatorRegistrationTests()
        {
            _mockLogger = new Mock<ILogger<CodeValidatorFactory>>();
            _registry = new ValidatorRegistry();
            _factory = new CodeValidatorFactory(_mockLogger.Object, _registry);
        }

        [Fact]
        public void RegisterValidator_ValidValidator_SuccessfullyRegisters()
        {
            // Arrange
            var mockValidator = new Mock<ICodeValidator>();
            var metadata = new ValidatorMetadata { Provider = "Test", Version = new Version(1, 0) };

            // Act
            var result = _registry.RegisterValidator("testlang", mockValidator.Object, metadata);

            // Assert
            Assert.True(result);
            Assert.True(_registry.HasValidator("testlang"));
            Assert.Same(mockValidator.Object, _registry.GetValidator("testlang"));
        }

        [Fact]
        public void UnregisterValidator_ExistingValidator_SuccessfullyUnregisters()
        {
            // Arrange
            var mockValidator = new Mock<ICodeValidator>();
            _registry.RegisterValidator("testlang", mockValidator.Object);

            // Act
            var result = _registry.UnregisterValidator("testlang");

            // Assert
            Assert.True(result);
            Assert.False(_registry.HasValidator("testlang"));
        }

        [Fact]
        public void GetValidator_RegisteredLanguage_ReturnsCorrectValidator()
        {
            // Arrange
            var mockValidator = new Mock<ICodeValidator>();
            _registry.RegisterValidator("testlang", mockValidator.Object);

            // Act
            var validator = _factory.GetValidator("testlang");

            // Assert
            Assert.Same(mockValidator.Object, validator);
        }

        [Fact]
        public void GetValidator_UnregisteredLanguage_ThrowsNotSupportedException()
        {
            // Act & Assert
            Assert.Throws<NotSupportedException>(() => _factory.GetValidator("nonexistent"));
        }

        [Fact]
        public void RegisterValidator_NullValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _registry.RegisterValidator("test", null));
        }

        [Fact]
        public void ValidatorRegistry_DefaultValidators_AreRegistered()
        {
            // Assert
            Assert.True(_factory.SupportsLanguage("csharp"));
            Assert.True(_factory.SupportsLanguage("javascript"));
            Assert.True(_factory.SupportsLanguage("python"));
        }

        [Fact]
        public void ValidatorEvents_AreTriggered()
        {
            // Arrange
            var registeredCalled = false;
            var unregisteredCalled = false;
            var mockValidator = new Mock<ICodeValidator>();

            _registry.ValidatorRegistered += (s, e) => registeredCalled = true;
            _registry.ValidatorUnregistered += (s, e) => unregisteredCalled = true;

            // Act
            _registry.RegisterValidator("testlang", mockValidator.Object);
            _registry.UnregisterValidator("testlang");

            // Assert
            Assert.True(registeredCalled);
            Assert.True(unregisteredCalled);
        }

        [Fact]
        public void GetAllValidators_ReturnsAllRegisteredValidators()
        {
            // Arrange
            var mockValidator1 = new Mock<ICodeValidator>();
            var mockValidator2 = new Mock<ICodeValidator>();

            _registry.RegisterValidator("lang1", mockValidator1.Object);
            _registry.RegisterValidator("lang2", mockValidator2.Object);

            // Act
            var validators = _registry.GetAllValidators();

            // Assert
            Assert.Equal(5, validators.Count); // 3 default + 2 new
            Assert.Contains(mockValidator1.Object, validators.Values);
            Assert.Contains(mockValidator2.Object, validators.Values);
        }

        [Fact]
        public void ValidatorMetadata_IsStoredAndRetrieved()
        {
            // Arrange
            var mockValidator = new Mock<ICodeValidator>();
            var metadata = new ValidatorMetadata
            {
                Provider = "Test Provider",
                Version = new Version(1, 2, 3),
                Description = "Test Description"
            };

            // Act
            _registry.RegisterValidator("testlang", mockValidator.Object, metadata);
            var retrievedMetadata = _registry.GetValidatorMetadata("testlang");

            // Assert
            Assert.NotNull(retrievedMetadata);
            Assert.Equal(metadata.Provider, retrievedMetadata.Provider);
            Assert.Equal(metadata.Version, retrievedMetadata.Version);
            Assert.Equal(metadata.Description, retrievedMetadata.Description);
        }
    }
}