using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable;

/// <summary>
/// Custom implementation of IOptionsMonitor that doesn't depend on Microsoft.Extensions.Configuration.
/// </summary>
/// <typeparam name="T">The type of options being monitored.</typeparam>
internal sealed class OptionsMonitorImpl<T> : IOptionsMonitor<T>, IDisposable
    where T : class
{
    private readonly Dictionary<string, T> _cache = [];
    private readonly Dictionary<string, T> _defaultValue = [];
    private readonly Dictionary<string, List<Action<T, string?>>> _listeners = [];
    private readonly Dictionary<string, FileSystemWatcher?> _watchers = [];
    private readonly Dictionary<string, WritableConfigurationOptions<T>> _optionsMap;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public OptionsMonitorImpl(IEnumerable<WritableConfigurationOptions<T>> options)
    {
        _optionsMap = options.ToDictionary(o => o.InstanceName, o => o);

        // Initialize cache and file watchers
        foreach (var opt in _optionsMap.Values)
        {
            var defaultValue = LoadConfiguration(opt.InstanceName);
            // Store the default value for IOptions
            _defaultValue[opt.InstanceName] = defaultValue;
            SetupFileWatcher(opt);
        }
    }

    /// <inheritdoc />
    public T CurrentValue => Get(MEOptions.DefaultName);

    /// <inheritdoc />
    public T Get(string? name)
    {
        name ??= MEOptions.DefaultName;
        if (_cache.TryGetValue(name, out var cached))
        {
            return cached;
        }
        // Load configuration if not cached
        return LoadConfiguration(name);
    }

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener)
    {
        foreach (var instanceName in GetInstanceNames())
        {
            if (!_listeners.TryGetValue(instanceName, out var value))
            {
                value = [];
                _listeners[instanceName] = value;
            }
            value.Add(listener);
        }
        return new ChangeTrackerDisposable(this, listener);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher?.Dispose();
        }
        _watchers.Clear();
        _listeners.Clear();
        _cache.Clear();
    }

    /// <summary>
    /// Gets all instance names for which options are configured. <br/>
    /// For use by <see cref="IOptionsSnapshot{TOptions}"/> implementation.
    /// </summary>
    internal IEnumerable<string> GetInstanceNames() => _optionsMap.Keys;

    /// <summary>
    /// Retrieves the default value associated with the specified instance name. <br/>
    /// For use by <see cref="IOptions{TOptions}"/> implementation.
    /// </summary>
    internal T GetDefaultValue(string instanceName)
    {
        if (_defaultValue.TryGetValue(instanceName, out var defaultValue))
        {
            return defaultValue;
        }
        throw new InvalidOperationException($"No default value found for instance: {instanceName}");
    }

    /// <summary>
    /// Updates the cached value for the specified instance name.
    /// This is called when SaveAsync is executed.
    /// </summary>
    internal void UpdateCache(string instanceName, T value)
    {
        _cache[instanceName] = value;
        NotifyListeners(instanceName, value);
    }

    /// <summary>
    /// Clears the cached value for the specified instance name.
    /// </summary>
    internal void ClearCache(string instanceName)
    {
        _cache.Remove(instanceName);
    }

    // Loads configuration from the provider and updates the cache.
    private T LoadConfiguration(string instanceName)
    {
        if (!_optionsMap.TryGetValue(instanceName, out var options))
        {
            throw new InvalidOperationException(
                $"No WritableConfigurationOptions<{typeof(T).Name}> found for instance: {instanceName}"
            );
        }

        _semaphore.Wait();
        try
        {
            // Use the provider to load configuration (provider will check file existence via its FileProvider)
            var value = options.Provider.LoadConfiguration<T>(options);
            _cache[instanceName] = value;
            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Sets up a FileSystemWatcher to monitor changes to the configuration file.
    private void SetupFileWatcher(WritableConfigurationOptions<T> options)
    {
        var filePath = options.ConfigFilePath;
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            _watchers[options.InstanceName] = null;
            return;
        }

        // Create directory if it doesn't exist
        if (!Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
                // If we can't create the directory, we can't watch it
                _watchers[options.InstanceName] = null;
                return;
            }
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            watcher.Changed += (sender, args) => OnFileChanged(options.InstanceName, args);
            watcher.Created += (sender, args) => OnFileChanged(options.InstanceName, args);
            watcher.Deleted += (sender, args) => OnFileChanged(options.InstanceName, args);
            watcher.Renamed += (sender, args) => OnFileChanged(options.InstanceName, args);

            _watchers[options.InstanceName] = watcher;
        }
        catch
        {
            // If file watching is not supported, continue without it
            _watchers[options.InstanceName] = null;
        }
    }

    // Called when the configuration file changes
    private void OnFileChanged(string instanceName, FileSystemEventArgs args)
    {
        // show log
        var options = _optionsMap[instanceName];

        if (options.ConfigFilePath != args.FullPath)
        {
            // Ignore changes to other files in the same directory
            // e.g. temporary file (foobar.json~ABCDEF.TMP)
            return;
        }

        var fileName = Path.GetFileName(options.ConfigFilePath);
        options.Logger?.LogInformation(
            "Configuration file change detected: {FileName} ({ChangeType})",
            fileName,
            args.ChangeType
        );

        // Reload and notify listeners with retry logic for file access conflicts
        try
        {
            var newValue = LoadConfigurationWithRetry(instanceName);
            NotifyListeners(instanceName, newValue);
        }
        catch (IOException)
        {
            // Clear cache because next Get() will try to reload
            _cache.Remove(instanceName);
        }
    }

    // Loads configuration with retry logic for file access conflicts
    private T LoadConfigurationWithRetry(string instanceName, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return LoadConfiguration(instanceName);
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                // Wait a bit before retrying (exponential backoff)
                Thread.Sleep(50 * (i + 1));
            }
        }
        // Final attempt without catching
        return LoadConfiguration(instanceName);
    }

    // Notifies all registered listeners of a configuration change
    private void NotifyListeners(string instanceName, T value)
    {
        if (_listeners.TryGetValue(instanceName, out var listeners))
        {
            foreach (var listener in listeners)
            {
                listener(value, instanceName);
            }
        }
    }

    private sealed class ChangeTrackerDisposable : IDisposable
    {
        private readonly OptionsMonitorImpl<T> _monitor;
        private readonly Action<T, string?> _listener;
        private bool _disposed;

        public ChangeTrackerDisposable(OptionsMonitorImpl<T> monitor, Action<T, string?> listener)
        {
            _monitor = monitor;
            _listener = listener;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _monitor._semaphore.Wait();
            try
            {
                foreach (var listeners in _monitor._listeners.Values)
                {
                    listeners.Remove(_listener);
                }
            }
            finally
            {
                _monitor._semaphore.Release();
            }

            _disposed = true;
        }
    }
}
