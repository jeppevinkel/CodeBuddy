using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Configuration;
using CodeBuddy.Core.Implementation.Configuration;

namespace CodeBuddy.Core.Implementation
{
    /// <summary>
    /// Manages application configuration including loading, saving, validation, and hot-reload support
    /// </summary>
    public class ConfigurationManager : IConfigurationManager, IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _configRoot;
        private readonly ConfigurationValidationManager _validationManager;
        private readonly ConfigurationMigrationManager _migrationManager;
        private readonly SecureConfigurationStorage _secureStorage;
        private readonly ConcurrentDictionary<string, object> _configurations;
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers;
        private readonly ConcurrentDictionary<string, List<Action<object>>> _changeCallbacks;
        private bool _disposed;

        public ConfigurationManager(
            ILogger logger,
            string configRoot,
            ConfigurationValidationManager validationManager,
            ConfigurationMigrationManager migrationManager)
        {
            _logger = logger;
            _configRoot = configRoot;
            _validationManager = validationManager;
            _migrationManager = migrationManager;
            _secureStorage = new SecureConfigurationStorage(
                logger,
                Path.Combine(configRoot, "keys", "config.key"),
                Path.Combine(configRoot, "secure")
            );
            _configurations = new ConcurrentDictionary<string, object>();
            _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
            _changeCallbacks = new ConcurrentDictionary<string, List<Action<object>>>();

            Directory.CreateDirectory(configRoot);
        }

        public async Task<T> GetConfigurationAsync<T>(string section) where T : BaseConfiguration, new()
        {
            var key = GetConfigKey<T>(section);

            if (_configurations.TryGetValue(key, out var existing) && existing is T config)
            {
                return config;
            }

            var configPath = GetConfigPath(section);
            T? loadedConfig = null;

            if (typeof(T).GetCustomAttributes(typeof(SecureStorageAttribute), true).Length > 0)
            {
                loadedConfig = await _secureStorage.LoadSecureConfigurationAsync<T>(section);
            }
            else if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                loadedConfig = JsonSerializer.Deserialize<T>(json);
            }

            if (loadedConfig == null)
            {
                loadedConfig = new T();
                await SaveConfigurationAsync(section, loadedConfig);
            }

            // Check for migrations
            if (_migrationManager.NeedsMigration(section, loadedConfig))
            {
                var migrationResult = await _migrationManager.MigrateConfiguration(section, loadedConfig);
                if (migrationResult.Success && migrationResult.Configuration is T migratedConfig)
                {
                    loadedConfig = migratedConfig;
                    await SaveConfigurationAsync(section, loadedConfig);
                }
                else
                {
                    _logger.LogWarning("Configuration migration failed for section {Section}: {Error}",
                        section, migrationResult.Error);
                }
            }

            // Validate configuration
            var validationResult = await _validationManager.ValidateConfigurationAsync(loadedConfig);
            if (validationResult != null && validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
            {
                throw new InvalidOperationException($"Configuration validation failed: {validationResult.ErrorMessage}");
            }

            _configurations[key] = loadedConfig;

            // Setup hot-reload if supported
            if (loadedConfig.IsReloadable && _validationManager.IsReloadable<T>())
            {
                SetupConfigurationWatcher(section, typeof(T));
            }

            return loadedConfig;
        }

        public async Task SaveConfigurationAsync<T>(string section, T configuration) where T : BaseConfiguration
        {
            var validationResult = await _validationManager.ValidateConfigurationAsync(configuration);
            if (validationResult != null && validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
            {
                throw new InvalidOperationException($"Configuration validation failed: {validationResult.ErrorMessage}");
            }

            configuration.LastModified = DateTime.UtcNow;
            var key = GetConfigKey<T>(section);

            if (typeof(T).GetCustomAttributes(typeof(SecureStorageAttribute), true).Length > 0)
            {
                await _secureStorage.SaveSecureConfigurationAsync(section, configuration);
            }
            else
            {
                var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(GetConfigPath(section), json);
            }

            _configurations[key] = configuration;
        }

        public void RegisterConfigurationChangeCallback<T>(string section, Action<T> callback) where T : BaseConfiguration
        {
            var key = GetConfigKey<T>(section);
            if (!_changeCallbacks.ContainsKey(key))
            {
                _changeCallbacks[key] = new List<Action<object>>();
            }

            _changeCallbacks[key].Add(obj => callback((T)obj));
        }

        private void SetupConfigurationWatcher(string section, Type configType)
        {
            var configPath = GetConfigPath(section);
            var key = GetConfigKey(configType, section);

            if (_watchers.ContainsKey(key))
            {
                return;
            }

            var watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(configPath),
                Filter = Path.GetFileName(configPath),
                NotifyFilter = NotifyFilters.LastWrite
            };

            var changeDebouncer = new Timer(async state =>
            {
                try
                {
                    if (_configurations.TryGetValue(key, out var currentConfig))
                    {
                        var json = await File.ReadAllTextAsync(configPath);
                        var newConfig = JsonSerializer.Deserialize(json, configType);

                        if (newConfig != null)
                        {
                            var validationResult = await _validationManager.ValidateConfigurationAsync((BaseConfiguration)newConfig);
                            if (validationResult == null || validationResult == System.ComponentModel.DataAnnotations.ValidationResult.Success)
                            {
                                _configurations[key] = newConfig;

                                if (_changeCallbacks.TryGetValue(key, out var callbacks))
                                {
                                    foreach (var callback in callbacks)
                                    {
                                        callback(newConfig);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling configuration change for {Section}", section);
                }
            });

            watcher.Changed += (sender, e) =>
            {
                changeDebouncer.Change(500, Timeout.Infinite);
            };

            watcher.EnableRaisingEvents = true;
            _watchers[key] = watcher;
        }

        private string GetConfigPath(string section)
        {
            return Path.Combine(_configRoot, $"{section}.json");
        }

        private string GetConfigKey<T>(string section)
        {
            return GetConfigKey(typeof(T), section);
        }

        private string GetConfigKey(Type type, string section)
        {
            return $"{type.FullName}:{section}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.Dispose();
                }
                _disposed = true;
            }
        }
    }
}