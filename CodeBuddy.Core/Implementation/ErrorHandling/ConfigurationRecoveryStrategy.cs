using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using CodeBuddy.Core.Models.Errors;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Implementation.ErrorHandling
{
    /// <summary>
    /// Strategy for recovering from plugin configuration failures by managing configuration backups and restores
    /// </summary>
    public class ConfigurationRecoveryStrategy : BaseErrorRecoveryStrategy
    {
        private readonly IConfigurationManager _configManager;
        private readonly IConfigurationMigrationManager _migrationManager;
        private readonly string _backupDirectory;
        private const string BackupExtension = ".backup";
        private const int MaxBackupVersions = 5;

        public ConfigurationRecoveryStrategy(
            RetryPolicy retryPolicy,
            IErrorAnalyticsService analytics,
            IConfigurationManager configManager,
            IConfigurationMigrationManager migrationManager) 
            : base(retryPolicy, analytics)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _migrationManager = migrationManager ?? throw new ArgumentNullException(nameof(migrationManager));
            _backupDirectory = Path.Combine(AppContext.BaseDirectory, "ConfigBackups");
            
            if (!Directory.Exists(_backupDirectory))
                Directory.CreateDirectory(_backupDirectory);
        }

        public override bool CanHandle(ValidationError error)
        {
            return error.Category == ErrorCategory.Configuration &&
                   error.Source == ErrorSource.Plugin;
        }

        public override async Task<bool> AttemptRecoveryAsync(ErrorRecoveryContext context)
        {
            try
            {
                if (!CanHandle(context.Error))
                    return false;

                var pluginId = context.Error.Metadata["PluginId"]?.ToString();
                if (string.IsNullOrEmpty(pluginId))
                    return false;

                // Find the most recent working backup
                var backupPath = await FindLatestValidBackupAsync(pluginId);
                if (string.IsNullOrEmpty(backupPath))
                {
                    await Analytics.TrackEventAsync("ConfigurationRecovery_NoValidBackup", 
                        new Dictionary<string, string> { { "PluginId", pluginId } });
                    return false;
                }

                // Restore the backup configuration
                var success = await RestoreConfigurationAsync(pluginId, backupPath);
                
                await Analytics.TrackEventAsync("ConfigurationRecovery_Attempt",
                    new Dictionary<string, string> 
                    { 
                        { "PluginId", pluginId },
                        { "Success", success.ToString() },
                        { "BackupPath", backupPath }
                    });

                return success;
            }
            catch (Exception ex)
            {
                TrackException(context, ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a backup of the current plugin configuration before changes
        /// </summary>
        public async Task BackupConfigurationAsync(string pluginId)
        {
            try
            {
                var config = await _configManager.GetPluginConfigurationAsync(pluginId);
                if (config == null)
                    return;

                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var backupPath = Path.Combine(_backupDirectory, $"{pluginId}_{timestamp}{BackupExtension}");
                
                await _configManager.SavePluginConfigurationAsync(pluginId, config, backupPath);
                await CleanupOldBackupsAsync(pluginId);
                
                await Analytics.TrackEventAsync("ConfigurationBackup_Created", 
                    new Dictionary<string, string> 
                    { 
                        { "PluginId", pluginId },
                        { "BackupPath", backupPath }
                    });
            }
            catch (Exception ex)
            {
                await Analytics.TrackExceptionAsync(new ErrorRecoveryContext 
                { 
                    Error = new ValidationError 
                    { 
                        Category = ErrorCategory.Configuration,
                        Message = "Failed to create configuration backup"
                    }
                }, ex);
            }
        }

        private async Task<string> FindLatestValidBackupAsync(string pluginId)
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, $"{pluginId}_*{BackupExtension}")
                                     .OrderByDescending(f => f);

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var config = await _configManager.LoadPluginConfigurationAsync(pluginId, backupFile);
                    if (await ValidateConfigurationAsync(config))
                        return backupFile;
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private async Task<bool> RestoreConfigurationAsync(string pluginId, string backupPath)
        {
            try
            {
                var config = await _configManager.LoadPluginConfigurationAsync(pluginId, backupPath);
                if (config == null)
                    return false;

                // Attempt to migrate the configuration if needed
                config = await _migrationManager.MigrateConfigurationAsync(config);
                
                // Save and validate the restored configuration
                await _configManager.SavePluginConfigurationAsync(pluginId, config);
                return await ValidateConfigurationAsync(config);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateConfigurationAsync(object config)
        {
            if (config == null)
                return false;

            try
            {
                // Validate using the configuration manager's validation logic
                return await _configManager.ValidateConfigurationAsync(config);
            }
            catch
            {
                return false;
            }
        }

        private async Task CleanupOldBackupsAsync(string pluginId)
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, $"{pluginId}_*{BackupExtension}")
                                     .OrderByDescending(f => f)
                                     .Skip(MaxBackupVersions);

            foreach (var file in backupFiles)
            {
                try
                {
                    File.Delete(file);
                    await Analytics.TrackEventAsync("ConfigurationBackup_Cleaned",
                        new Dictionary<string, string> { { "BackupFile", file } });
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }
}