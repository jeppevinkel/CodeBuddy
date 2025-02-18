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
        private readonly string _compressedDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private const int COMPRESSION_AGE_DAYS = 30;

        public FileBasedVersionHistoryStorage(string storageDirectory)
        {
            _storageDirectory = storageDirectory;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            _compressedDirectory = Path.Combine(_storageDirectory, "compressed");
            
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
            if (!Directory.Exists(_compressedDirectory))
            {
                Directory.CreateDirectory(_compressedDirectory);
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
            string compressedPath = GetCompressedFilePath(versionId);
            
            if (File.Exists(filePath))
            {
                string json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<ConfigurationVersionHistory>(json, _jsonOptions);
            }
            else if (File.Exists(compressedPath))
            {
                using var compressedStream = File.OpenRead(compressedPath);
                using var decompressStream = new System.IO.Compression.GZipStream(
                    compressedStream, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(decompressStream);
                string json = await reader.ReadToEndAsync();
                return JsonSerializer.Deserialize<ConfigurationVersionHistory>(json, _jsonOptions);
            }
            
            return null;
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
                var compressionDate = DateTime.UtcNow.AddDays(-COMPRESSION_AGE_DAYS);
                var deletionDate = DateTime.UtcNow.AddDays(-retentionDays);
                var versions = await LoadVersionIndexAsync();

                // Compress old versions
                var versionsToCompress = versions
                    .Where(v => v.Timestamp < compressionDate && v.Timestamp >= deletionDate)
                    .ToList();

                foreach (var version in versionsToCompress)
                {
                    string filePath = GetVersionFilePath(version.VersionId);
                    string compressedPath = GetCompressedFilePath(version.VersionId);
                    
                    if (File.Exists(filePath) && !File.Exists(compressedPath))
                    {
                        string json = await File.ReadAllTextAsync(filePath);
                        using var compressedFile = File.Create(compressedPath);
                        using var compressStream = new System.IO.Compression.GZipStream(
                            compressedFile, System.IO.Compression.CompressionMode.Compress);
                        using var writer = new StreamWriter(compressStream);
                        await writer.WriteAsync(json);
                        File.Delete(filePath);
                    }
                }

                // Delete old versions
                var versionsToRemove = versions.Where(v => v.Timestamp < deletionDate).ToList();
                foreach (var version in versionsToRemove)
                {
                    string filePath = GetVersionFilePath(version.VersionId);
                    string compressedPath = GetCompressedFilePath(version.VersionId);
                    
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    if (File.Exists(compressedPath))
                    {
                        File.Delete(compressedPath);
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

        private string GetCompressedFilePath(string versionId)
        {
            return Path.Combine(_compressedDirectory, $"version_{versionId}.json.gz");
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