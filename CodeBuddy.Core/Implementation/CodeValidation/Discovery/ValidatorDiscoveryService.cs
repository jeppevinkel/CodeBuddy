using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using CodeBuddy.Core.Implementation.CodeValidation.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation.CodeValidation.Discovery
{
    public class ValidatorDiscoveryService : IValidatorDiscoveryService
    {
        private readonly ILogger<ValidatorDiscoveryService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IValidatorRegistry _validatorRegistry;
        private readonly ValidatorDiscoveryConfig _config;
        private readonly Dictionary<string, AssemblyLoadContext> _loadContexts;
        private readonly FileSystemWatcher _watcher;

        public ValidatorDiscoveryService(
            ILogger<ValidatorDiscoveryService> logger,
            IConfiguration configuration,
            IValidatorRegistry validatorRegistry)
        {
            _logger = logger;
            _configuration = configuration;
            _validatorRegistry = validatorRegistry;
            _config = configuration.GetSection("ValidatorDiscovery").Get<ValidatorDiscoveryConfig>();
            _loadContexts = new Dictionary<string, AssemblyLoadContext>();
            
            InitializeFileWatcher();
        }

        private void InitializeFileWatcher()
        {
            _watcher = new FileSystemWatcher(_config.ValidatorPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.dll",
                EnableRaisingEvents = true
            };

            _watcher.Created += OnValidatorAssemblyChanged;
            _watcher.Changed += OnValidatorAssemblyChanged;
            _watcher.Deleted += OnValidatorAssemblyRemoved;
        }

        public void DiscoverAndRegisterValidators()
        {
            var validatorFiles = Directory.GetFiles(_config.ValidatorPath, "*.dll", 
                _config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            foreach (var file in validatorFiles)
            {
                try
                {
                    LoadAndRegisterValidators(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load validators from {File}", file);
                }
            }
        }

        private void LoadAndRegisterValidators(string assemblyPath)
        {
            var loadContext = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(assemblyPath), true);
            _loadContexts[assemblyPath] = loadContext;

            using (var stream = File.OpenRead(assemblyPath))
            {
                var assembly = loadContext.LoadFromStream(stream);
                var validatorTypes = assembly.GetTypes()
                    .Where(t => typeof(ICodeValidator).IsAssignableFrom(t) && 
                               !t.IsInterface && !t.IsAbstract);

                foreach (var validatorType in validatorTypes)
                {
                    var metadata = validatorType.GetCustomAttribute<ValidatorMetadataAttribute>();
                    if (metadata == null)
                    {
                        _logger.LogWarning("Validator {Type} is missing metadata attribute", validatorType.Name);
                        continue;
                    }

                    if (ValidateDependencies(metadata.RequiredDependencies))
                    {
                        var validator = (ICodeValidator)Activator.CreateInstance(validatorType);
                        _validatorRegistry.Register(validator, metadata);
                        _logger.LogInformation("Registered validator: {Type}", validatorType.Name);
                    }
                }
            }
        }

        private bool ValidateDependencies(string[] dependencies)
        {
            if (dependencies == null || !dependencies.Any())
                return true;

            foreach (var dependency in dependencies)
            {
                if (!File.Exists(Path.Combine(_config.DependencyPath, dependency)))
                {
                    _logger.LogError("Missing dependency: {Dependency}", dependency);
                    return false;
                }
            }

            return true;
        }

        private void OnValidatorAssemblyChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Wait briefly to ensure file is completely written
                System.Threading.Thread.Sleep(100);
                
                if (_loadContexts.TryGetValue(e.FullPath, out var context))
                {
                    context.Unload();
                    _loadContexts.Remove(e.FullPath);
                }

                LoadAndRegisterValidators(e.FullPath);
                _logger.LogInformation("Reloaded validators from {File}", e.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload validators from {File}", e.Name);
            }
        }

        private void OnValidatorAssemblyRemoved(object sender, FileSystemEventArgs e)
        {
            if (_loadContexts.TryGetValue(e.FullPath, out var context))
            {
                context.Unload();
                _loadContexts.Remove(e.FullPath);
                _logger.LogInformation("Unloaded validators from {File}", e.Name);
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            foreach (var context in _loadContexts.Values)
            {
                context.Unload();
            }
            _loadContexts.Clear();
        }
    }

    public class ValidatorDiscoveryConfig
    {
        public string ValidatorPath { get; set; }
        public string DependencyPath { get; set; }
        public bool IncludeSubdirectories { get; set; }
        public bool EnableHotReload { get; set; }
        public int ReloadDelayMs { get; set; } = 100;
    }
}