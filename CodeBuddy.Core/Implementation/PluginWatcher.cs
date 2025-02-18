using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CodeBuddy.Core.Implementation;

/// <summary>
/// Monitors plugin directories for changes and triggers reload events
/// </summary>
public class PluginWatcher : IDisposable
{
    private readonly ILogger<PluginWatcher> _logger;
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, DateTime> _lastModified;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2);
    private readonly Action<string> _onPluginChanged;
    private bool _disposed;

    public PluginWatcher(ILogger<PluginWatcher> logger, Action<string> onPluginChanged)
    {
        _logger = logger;
        _onPluginChanged = onPluginChanged;
        _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
        _lastModified = new ConcurrentDictionary<string, DateTime>();
    }

    /// <summary>
    /// Starts monitoring a plugin directory
    /// </summary>
    public Task StartWatchingAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
            return Task.CompletedTask;
        }

        if (_watchers.ContainsKey(directory))
        {
            _logger.LogInformation("Already watching directory: {Directory}", directory);
            return Task.CompletedTask;
        }

        var watcher = new FileSystemWatcher(directory)
        {
            Filter = "*.dll",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnPluginFileChanged;
        watcher.Created += OnPluginFileChanged;
        watcher.Renamed += OnPluginFileRenamed;

        _watchers.TryAdd(directory, watcher);
        _logger.LogInformation("Started watching directory: {Directory}", directory);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops monitoring all plugin directories
    /// </summary>
    public Task StopWatchingAsync()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _lastModified.Clear();

        _logger.LogInformation("Stopped watching all directories");
        return Task.CompletedTask;
    }

    private void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        HandlePluginChange(e.FullPath);
    }

    private void OnPluginFileRenamed(object sender, RenamedEventArgs e)
    {
        HandlePluginChange(e.FullPath);
    }

    private void HandlePluginChange(string fullPath)
    {
        try
        {
            // Debounce multiple events for the same file
            var now = DateTime.UtcNow;
            if (_lastModified.TryGetValue(fullPath, out var lastMod) && 
                now - lastMod < _debounceInterval)
            {
                return;
            }

            _lastModified.AddOrUpdate(fullPath, now, (_, _) => now);

            // Wait briefly to ensure file is not locked
            Task.Delay(500).ContinueWith(_ =>
            {
                try
                {
                    // Ensure file exists and is accessible
                    if (!File.Exists(fullPath))
                    {
                        return;
                    }

                    using (File.OpenRead(fullPath))
                    {
                        // File is accessible
                    }

                    _onPluginChanged.Invoke(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accessing changed plugin file: {Path}", fullPath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling plugin change: {Path}", fullPath);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        _lastModified.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}