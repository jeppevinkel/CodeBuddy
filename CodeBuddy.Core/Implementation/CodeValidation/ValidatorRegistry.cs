using Microsoft.Extensions.Logging;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using CodeBuddy.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CodeBuddy.Core.Implementation.CodeValidation;

public class ValidatorRegistry : IValidatorRegistry, IDisposable
{
    private readonly ILogger<ValidatorRegistry> _logger;
    private readonly ConcurrentDictionary<string, ValidatorInfo> _validators;
    private readonly Dictionary<string, FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, string> _assemblyHashes;
    private readonly List<string> _watchedPaths;
    private readonly ValidatorRegistryConfig _config;
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;
    private readonly object _lock = new();

    public ValidatorRegistry(ILogger<ValidatorRegistry> logger, ValidatorRegistryConfig config)
    {
        _logger = logger;
        _config = config;
        _validators = new ConcurrentDictionary<string, ValidatorInfo>(StringComparer.OrdinalIgnoreCase);
        _watchers = new Dictionary<string, FileSystemWatcher>();
        _assemblyHashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _watchedPaths = new List<string>();

        if (_config.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecks, null, _config.HealthCheckIntervalMs, _config.HealthCheckIntervalMs);
        }
        
        if (_config.AutoDiscoveryPaths != null)
        {
            foreach (var path in _config.AutoDiscoveryPaths)
            {
                if (_config.EnableHotReload)
                {
                    ConfigureDirectoryWatcher(path);
                }
                ScanDirectoryForValidators(path);
            }
        }
    }

    private void ConfigureDirectoryWatcher(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Directory {Path} does not exist", path);
            return;
        }

        var watcher = new FileSystemWatcher(path)
        {
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnValidatorAssemblyChanged;
        watcher.Created += OnValidatorAssemblyChanged;
        watcher.Deleted += OnValidatorAssemblyDeleted;

        _watchers[path] = watcher;
        _watchedPaths.Add(path);
        _logger.LogInformation("Configured directory watcher for {Path}", path);
    }

    private async void OnValidatorAssemblyChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var assemblyPath = e.FullPath;
            if (!File.Exists(assemblyPath)) return;

            // Wait for file to be released
            await Task.Delay(_config.FileChangeDelayMs);

            var newHash = await CalculateFileHashAsync(assemblyPath);
            if (_assemblyHashes.TryGetValue(assemblyPath, out var existingHash) && existingHash == newHash)
            {
                return; // File hasn't really changed
            }

            _logger.LogInformation("Validator assembly changed: {Path}", assemblyPath);
            _assemblyHashes[assemblyPath] = newHash;

            var assembly = Assembly.LoadFrom(assemblyPath);
            await Task.Run(() => RegisterValidatorsFromAssembly(assembly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing validator assembly change");
        }
    }

    private void OnValidatorAssemblyDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("Validator assembly deleted: {Path}", e.FullPath);
            _assemblyHashes.TryRemove(e.FullPath, out _);

            // Find and remove validators from this assembly
            var validatorsToRemove = _validators.Values
                .Where(v => v.AssemblyPath == e.FullPath)
                .Select(v => v.Language)
                .ToList();

            foreach (var language in validatorsToRemove)
            {
                UnregisterValidator(language);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing validator assembly deletion");
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hash);
    }

    private void ScanDirectoryForValidators(string path)
    {
        try
        {
            var assemblyFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);
            foreach (var assemblyFile in assemblyFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyFile);
                    RegisterValidatorsFromAssembly(assembly);
                    _assemblyHashes[assemblyFile] = CalculateFileHashAsync(assemblyFile).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading assembly {Path}", assemblyFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory {Path} for validators", path);
        }
    }

    private void PerformHealthChecks(object? state)
    {
        try
        {
            foreach (var validator in _validators.Values)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var instance = Activator.CreateInstance(validator.ValidatorType) as ICodeValidator;
                    sw.Stop();

                    var currentProcess = Process.GetCurrentProcess();
                    
                    validator.HealthInfo.LastChecked = DateTime.UtcNow;
                    validator.HealthInfo.IsHealthy = instance != null;
                    validator.HealthInfo.LoadTime = sw.Elapsed;
                    validator.HealthInfo.MemoryUsageBytes = currentProcess.WorkingSet64;
                    validator.HealthInfo.LastError = null;
                }
                catch (Exception ex)
                {
                    validator.HealthInfo.IsHealthy = false;
                    validator.HealthInfo.LastError = ex;
                    _logger.LogError(ex, "Health check failed for validator {Language}", validator.Language);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing validator health checks");
        }
    }

    public void RegisterValidator(Type validatorType)
    {
        if (!typeof(ICodeValidator).IsAssignableFrom(validatorType))
        {
            throw new ArgumentException($"Type {validatorType.FullName} does not implement ICodeValidator");
        }

        if (!typeof(IValidatorCapabilities).IsAssignableFrom(validatorType))
        {
            throw new ArgumentException($"Type {validatorType.FullName} does not implement IValidatorCapabilities");
        }

        var instance = Activator.CreateInstance(validatorType) as IValidatorCapabilities;
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of {validatorType.FullName}");
        }

        var validatorInfo = new ValidatorInfo(instance.Language, validatorType, instance);
        if (_validators.TryAdd(instance.Language, validatorInfo))
        {
            _logger.LogInformation("Registered validator for language: {Language}", instance.Language);
        }
    }

    public void RegisterValidator<T>() where T : ICodeValidator
    {
        RegisterValidator(typeof(T));
    }

    public void RegisterValidatorsFromAssembly(Assembly assembly)
    {
        var validatorTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(ICodeValidator).IsAssignableFrom(t)
                && typeof(IValidatorCapabilities).IsAssignableFrom(t));

        foreach (var type in validatorTypes)
        {
            try
            {
                RegisterValidator(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register validator type: {Type}", type.FullName);
            }
        }
    }

    public void UnregisterValidator(string language)
    {
        if (_validators.TryRemove(language, out var info))
        {
            _logger.LogInformation("Unregistered validator for language: {Language}", language);
        }
    }

    public bool IsValidatorRegistered(string language)
    {
        return _validators.ContainsKey(language);
    }

    public IReadOnlyCollection<ValidatorInfo> GetRegisteredValidators()
    {
        return _validators.Values.ToList().AsReadOnly();
    }

    public ValidatorInfo GetValidatorInfo(string language)
    {
        if (_validators.TryGetValue(language, out var info))
        {
            return info;
        }

        throw new KeyNotFoundException($"No validator registered for language: {language}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _healthCheckTimer?.Dispose();

        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnValidatorAssemblyChanged;
            watcher.Created -= OnValidatorAssemblyChanged;
            watcher.Deleted -= OnValidatorAssemblyDeleted;
            watcher.Dispose();
        }

        _watchers.Clear();
        _disposed = true;
    }
}