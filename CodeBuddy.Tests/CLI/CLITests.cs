using System;
using System.Threading.Tasks;
using CodeBuddy.CLI;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeBuddy.Tests.CLI
{
    public class CLITests
    {
        [Fact]
        public async Task ValidateCommand_WithValidPath_ReturnsSuccess()
        {
            // Arrange
            var ruleManagerMock = new Mock<IRuleManager>();
            ruleManagerMock.Setup(rm => rm.ValidateAsync(It.IsAny<ValidationOptions>()))
                .ReturnsAsync(new ValidationResult { IsSuccess = true });

            var args = new[] { "validate", "-p", "test/path" };

            // Act
            var result = await Program.Main(args);

            // Assert
            Assert.Equal(0, result);
            ruleManagerMock.Verify(rm => rm.ValidateAsync(It.Is<ValidationOptions>(
                vo => vo.FilePath == "test/path")), Times.Once);
        }

        [Fact]
        public async Task ListRulesCommand_WithLanguageFilter_ReturnsSuccess()
        {
            // Arrange
            var ruleManagerMock = new Mock<IRuleManager>();
            ruleManagerMock.Setup(rm => rm.GetAvailableRulesAsync(It.IsAny<string>()))
                .ReturnsAsync(new[] { new CustomRule { Id = "R001", Description = "Test Rule" } });

            var args = new[] { "list-rules", "-l", "csharp" };

            // Act
            var result = await Program.Main(args);

            // Assert
            Assert.Equal(0, result);
            ruleManagerMock.Verify(rm => rm.GetAvailableRulesAsync("csharp"), Times.Once);
        }

        [Fact]
        public async Task ValidateCommand_WithInvalidPath_ReturnsError()
        {
            // Arrange
            var ruleManagerMock = new Mock<IRuleManager>();
            ruleManagerMock.Setup(rm => rm.ValidateAsync(It.IsAny<ValidationOptions>()))
                .ThrowsAsync(new FileNotFoundException("File not found"));

            var args = new[] { "validate", "-p", "invalid/path" };

            // Act
            var result = await Program.Main(args);

            // Assert
            Assert.Equal(1, result);
        }
    }
}