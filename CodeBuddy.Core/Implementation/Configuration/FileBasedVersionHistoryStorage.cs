using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// File-based implementation of configuration version history storage
    /// </summary>
    public class FileBasedVersionHistoryStorage : IConfigurationVersionHistoryStorage
    {
        private readonly string _storageDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public FileBasedVersionHistoryStorage(string storageDirectory)
        {
            _storageDirectory = storageDirectory;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        public async Task StoreVersionAsync(ConfigurationVersionHistory versionHistory)
        {
            await _lock.WaitAsync();
            try
            {
                string filePath = GetVersionFilePath(versionHistory.VersionId);
                string json = JsonSerializer.Serialize(versionHistory, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                // Update index file
                await UpdateIndexAsync(versionHistory);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Dictionary<string, object>> GetConfigurationAtTimeAsync(DateTime timestamp)
        {
            var versions = await LoadVersionIndexAsync();
            var version = versions
                .Where(v => v.Timestamp <= timestamp)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            if (version == null)
            {
                return new Dictionary<string, object>();
            }

            var fullVersion = await GetVersionAsync(version.VersionId);
            return fullVersion.ConfigurationState;
        }

        public async Task<ConfigurationVersionHistory> GetVersionAsync(string versionId)
        {
            string filePath = GetVersionFilePath(versionId);
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<ConfigurationVersionHistory>(json, _jsonOptions);
        }

        public async Task<List<ConfigurationVersionHistory>> ListVersionsAsync(DateTime startTime, DateTime endTime)
        {
            var versions = await LoadVersionIndexAsync();
            return versions
                .Where(v => v.Timestamp >= startTime && v.Timestamp <= endTime)
                .OrderByDescending(v => v.Timestamp)
                .ToList();
        }

        public async Task<Dictionary<string, ConfigurationValueChange>> CompareVersionsAsync(string versionId1, string versionId2)
        {
            var version1 = await GetVersionAsync(versionId1);
            var version2 = await GetVersionAsync(versionId2);

            if (version1 == null || version2 == null)
            {
                throw new ArgumentException("One or both versions not found");
            }

            var differences = new Dictionary<string, ConfigurationValueChange>();

            // Compare configuration states
            foreach (var key in version1.ConfigurationState.Keys.Union(version2.ConfigurationState.Keys))
            {
                version1.ConfigurationState.TryGetValue(key, out var value1);
                version2.ConfigurationState.TryGetValue(key, out var value2);

                if (!Equals(value1, value2))
                {
                    differences[key] = new ConfigurationValueChange
                    {
                        PreviousValue = value1,
                        NewValue = value2
                    };
                }
            }

            return differences;
        }

        public async Task CleanupHistoryAsync(int retentionDays)
        {
            await _lock.WaitAsync();
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var versions = await LoadVersionIndexAsync();

                var versionsToRemove = versions.Where(v => v.Timestamp < cutoffDate).ToList();
                foreach (var version in versionsToRemove)
                {
                    string filePath = GetVersionFilePath(version.VersionId);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }

                // Update index without removed versions
                await SaveVersionIndexAsync(versions.Except(versionsToRemove).ToList());
            }
            finally
            {
                _lock.Release();
            }
        }

        private string GetVersionFilePath(string versionId)
        {
            return Path.Combine(_storageDirectory, $"version_{versionId}.json");
        }

        private string GetIndexFilePath()
        {
            return Path.Combine(_storageDirectory, "version_index.json");
        }

        private async Task UpdateIndexAsync(ConfigurationVersionHistory version)
        {
            var versions = await LoadVersionIndexAsync();
            versions.RemoveAll(v => v.VersionId == version.VersionId);
            versions.Add(version);
            await SaveVersionIndexAsync(versions);
        }

        private async Task<List<ConfigurationVersionHistory>> LoadVersionIndexAsync()
        {
            string indexPath = GetIndexFilePath();
            if (!File.Exists(indexPath))
            {
                return new List<ConfigurationVersionHistory>();
            }

            string json = await File.ReadAllTextAsync(indexPath);
            return JsonSerializer.Deserialize<List<ConfigurationVersionHistory>>(json, _jsonOptions);
        }

        private async Task SaveVersionIndexAsync(List<ConfigurationVersionHistory> versions)
        {
            string json = JsonSerializer.Serialize(versions.OrderByDescending(v => v.Timestamp), _jsonOptions);
            await File.WriteAllTextAsync(GetIndexFilePath(), json);
        }
    }
}