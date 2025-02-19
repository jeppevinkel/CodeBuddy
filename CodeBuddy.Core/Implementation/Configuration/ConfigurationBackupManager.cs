using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace CodeBuddy.Core.Implementation.Configuration
{
    /// <summary>
    /// Manages configuration backups and restoration
    /// </summary>
    public class ConfigurationBackupManager
    {
        private readonly ILogger _logger;
        private readonly string _configRoot;
        private readonly string _backupRoot;
        private readonly SecureConfigurationStorage _secureStorage;

        public ConfigurationBackupManager(
            ILogger logger,
            string configRoot,
            SecureConfigurationStorage secureStorage)
        {
            _logger = logger;
            _configRoot = configRoot;
            _backupRoot = Path.Combine(configRoot, "backups");
            _secureStorage = secureStorage;

            Directory.CreateDirectory(_backupRoot);
        }

        /// <summary>
        /// Creates a backup of all configuration files
        /// </summary>
        public async Task<string> CreateBackupAsync(string backupName)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var backupFileName = $"{backupName}_{timestamp}.zip";
            var backupPath = Path.Combine(_backupRoot, backupFileName);

            try
            {
                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Backup regular config files
                    foreach (var file in Directory.GetFiles(_configRoot, "*.json"))
                    {
                        var relativePath = Path.GetRelativePath(_configRoot, file);
                        archive.CreateEntryFromFile(file, relativePath);
                    }

                    // Backup secure config files
                    var secureConfigPath = Path.Combine(_configRoot, "secure");
                    if (Directory.Exists(secureConfigPath))
                    {
                        foreach (var file in Directory.GetFiles(secureConfigPath, "*.encrypted"))
                        {
                            var relativePath = Path.Combine("secure", Path.GetRelativePath(secureConfigPath, file));
                            archive.CreateEntryFromFile(file, relativePath);
                        }
                    }

                    // Backup encryption keys
                    var keyPath = Path.Combine(_configRoot, "keys", "config.key");
                    if (File.Exists(keyPath))
                    {
                        archive.CreateEntryFromFile(keyPath, "keys/config.key");
                    }

                    // Create backup manifest
                    var manifest = new
                    {
                        CreatedAt = DateTime.UtcNow,
                        Name = backupName,
                        Files = new List<string>()
                    };

                    var manifestEntry = archive.CreateEntry("manifest.json");
                    using (var writer = new StreamWriter(manifestEntry.Open()))
                    {
                        await writer.WriteAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }

                _logger.LogInformation("Created configuration backup: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating configuration backup");
                throw;
            }
        }

        /// <summary>
        /// Restores configuration from a backup file
        /// </summary>
        public async Task RestoreFromBackupAsync(string backupPath)
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            try
            {
                // Create temporary restore directory
                var restoreDir = Path.Combine(_backupRoot, "restore_temp");
                if (Directory.Exists(restoreDir))
                {
                    Directory.Delete(restoreDir, true);
                }
                Directory.CreateDirectory(restoreDir);

                // Extract backup
                ZipFile.ExtractToDirectory(backupPath, restoreDir);

                // Verify manifest
                var manifestPath = Path.Combine(restoreDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    throw new InvalidOperationException("Invalid backup file: missing manifest");
                }

                // Copy files to config directory
                foreach (var file in Directory.GetFiles(restoreDir, "*.json"))
                {
                    if (Path.GetFileName(file) != "manifest.json")
                    {
                        var destPath = Path.Combine(_configRoot, Path.GetFileName(file));
                        File.Copy(file, destPath, true);
                    }
                }

                // Restore secure configs
                var secureSourcePath = Path.Combine(restoreDir, "secure");
                if (Directory.Exists(secureSourcePath))
                {
                    var secureDestPath = Path.Combine(_configRoot, "secure");
                    Directory.CreateDirectory(secureDestPath);

                    foreach (var file in Directory.GetFiles(secureSourcePath, "*.encrypted"))
                    {
                        var destPath = Path.Combine(secureDestPath, Path.GetFileName(file));
                        File.Copy(file, destPath, true);
                    }
                }

                // Restore encryption key
                var keySourcePath = Path.Combine(restoreDir, "keys", "config.key");
                if (File.Exists(keySourcePath))
                {
                    var keyDestPath = Path.Combine(_configRoot, "keys", "config.key");
                    Directory.CreateDirectory(Path.GetDirectoryName(keyDestPath));
                    File.Copy(keySourcePath, keyDestPath, true);
                }

                _logger.LogInformation("Successfully restored configuration from backup: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring configuration from backup");
                throw;
            }
            finally
            {
                // Cleanup temporary directory
                var restoreDir = Path.Combine(_backupRoot, "restore_temp");
                if (Directory.Exists(restoreDir))
                {
                    Directory.Delete(restoreDir, true);
                }
            }
        }

        /// <summary>
        /// Lists available configuration backups
        /// </summary>
        public IEnumerable<ConfigurationBackupInfo> ListBackups()
        {
            var backups = new List<ConfigurationBackupInfo>();
            
            foreach (var file in Directory.GetFiles(_backupRoot, "*.zip"))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(file);
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        using var reader = new StreamReader(manifestEntry.Open());
                        var manifest = JsonSerializer.Deserialize<ConfigurationBackupInfo>(reader.ReadToEnd());
                        if (manifest != null)
                        {
                            manifest.FilePath = file;
                            backups.Add(manifest);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading backup manifest from {File}", file);
                }
            }

            return backups;
        }
    }

    public class ConfigurationBackupInfo
    {
        public DateTime CreatedAt { get; set; }
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public List<string> Files { get; set; } = new();
    }
}