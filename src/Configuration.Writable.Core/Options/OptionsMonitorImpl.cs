using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        _semaphore.Wait();
        try
        {
            foreach (var instanceName in _optionsMap.Keys)
            {
                if (!_listeners.ContainsKey(instanceName))
                {
                    _listeners[instanceName] = [];
                }
                _listeners[instanceName].Add(listener);
            }
        }
        finally
        {
            _semaphore.Release();
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
            // Use the provider to load configuration (provider will check file existence via its FileWriter)
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

            watcher.Changed += (sender, args) => OnFileChanged(options.InstanceName);
            watcher.Created += (sender, args) => OnFileChanged(options.InstanceName);
            watcher.Deleted += (sender, args) => OnFileChanged(options.InstanceName);
            watcher.Renamed += (sender, args) => OnFileChanged(options.InstanceName);

            _watchers[options.InstanceName] = watcher;
        }
        catch
        {
            // If file watching is not supported, continue without it
            _watchers[options.InstanceName] = null;
        }
    }

    // Called when the configuration file changes
    private void OnFileChanged(string instanceName)
    {
        // Clear cache to force reload on next Get
        _cache.Remove(instanceName);

        // Reload and notify listeners
        var newValue = LoadConfiguration(instanceName);
        NotifyListeners(instanceName, newValue);
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
