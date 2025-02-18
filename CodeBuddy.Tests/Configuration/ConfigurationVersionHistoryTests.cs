using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation.Configuration;
using CodeBuddy.Core.Implementation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeBuddy.Tests.Configuration
{
    [TestClass]
    public class ConfigurationVersionHistoryTests
    {
        private string _testStorageDirectory;
        private FileBasedVersionHistoryStorage _storage;
        private ConfigurationManager _configManager;

        [TestInitialize]
        public void Setup()
        {
            _testStorageDirectory = Path.Combine(Path.GetTempPath(), "ConfigVersionHistoryTests");
            if (Directory.Exists(_testStorageDirectory))
            {
                Directory.Delete(_testStorageDirectory, true);
            }
            Directory.CreateDirectory(_testStorageDirectory);

            _storage = new FileBasedVersionHistoryStorage(_testStorageDirectory);
            _configManager = new ConfigurationManager(_storage, "test-user");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testStorageDirectory))
            {
                Directory.Delete(_testStorageDirectory, true);
            }
        }

        [TestMethod]
        public async Task UpdateConfiguration_StoresVersionHistory()
        {
            // Arrange
            var changes = new Dictionary<string, object>
            {
                { "setting1", "value1" },
                { "setting2", 42 }
            };

            // Act
            var result = await _configManager.UpdateConfigurationAsync(
                changes,
                "Test update",
                "migration-1",
                new List<string> { "Component1" });

            // Assert
            Assert.IsTrue(result);
            
            var history = await _configManager.GetVersionHistoryAsync(
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
            
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("test-user", history[0].ChangedBy);
            Assert.AreEqual("Test update", history[0].ChangeReason);
            Assert.AreEqual("migration-1", history[0].MigrationId);
            CollectionAssert.AreEqual(new[] { "Component1" }, history[0].AffectedComponents);
        }

        [TestMethod]
        public async Task GetConfigurationAtTime_ReturnsCorrectVersion()
        {
            // Arrange
            var initialChanges = new Dictionary<string, object>
            {
                { "setting1", "initial" }
            };
            await _configManager.UpdateConfigurationAsync(initialChanges);

            var timestamp = DateTime.UtcNow;

            var updatedChanges = new Dictionary<string, object>
            {
                { "setting1", "updated" }
            };
            await _configManager.UpdateConfigurationAsync(updatedChanges);

            // Act
            var configAtTime = await _configManager.GetConfigurationAtTimeAsync(timestamp);

            // Assert
            Assert.AreEqual("initial", configAtTime["setting1"]);
        }

        [TestMethod]
        public async Task CompareVersions_ShowsCorrectDifferences()
        {
            // Arrange
            var changes1 = new Dictionary<string, object>
            {
                { "setting1", "value1" },
                { "setting2", 42 }
            };
            await _configManager.UpdateConfigurationAsync(changes1);
            var history1 = await _configManager.GetVersionHistoryAsync(
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
            var version1Id = history1[0].VersionId;

            var changes2 = new Dictionary<string, object>
            {
                { "setting1", "value2" },
                { "setting3", true }
            };
            await _configManager.UpdateConfigurationAsync(changes2);
            var history2 = await _configManager.GetVersionHistoryAsync(
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
            var version2Id = history2[0].VersionId;

            // Act
            var differences = await _configManager.CompareVersionsAsync(version1Id, version2Id);

            // Assert
            Assert.AreEqual(3, differences.Count);
            Assert.AreEqual("value1", differences["setting1"].PreviousValue);
            Assert.AreEqual("value2", differences["setting1"].NewValue);
            Assert.AreEqual(42, differences["setting2"].PreviousValue);
            Assert.IsNull(differences["setting2"].NewValue);
            Assert.IsNull(differences["setting3"].PreviousValue);
            Assert.AreEqual(true, differences["setting3"].NewValue);
        }

        [TestMethod]
        public async Task RollbackToVersion_RestoresPreviousState()
        {
            // Arrange
            var initialChanges = new Dictionary<string, object>
            {
                { "setting1", "initial" },
                { "setting2", 42 }
            };
            await _configManager.UpdateConfigurationAsync(initialChanges);
            var history = await _configManager.GetVersionHistoryAsync(
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(1));
            var initialVersionId = history[0].VersionId;

            var updatedChanges = new Dictionary<string, object>
            {
                { "setting1", "updated" },
                { "setting2", 100 }
            };
            await _configManager.UpdateConfigurationAsync(updatedChanges);

            // Act
            var rollbackResult = await _configManager.RollbackToVersionAsync(initialVersionId);
            var currentConfig = await _configManager.GetConfigurationAtTimeAsync(DateTime.UtcNow);

            // Assert
            Assert.IsTrue(rollbackResult);
            Assert.AreEqual("initial", currentConfig["setting1"]);
            Assert.AreEqual(42, currentConfig["setting2"]);
        }
    }
}