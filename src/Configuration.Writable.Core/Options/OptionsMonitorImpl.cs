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
    where T : class, new()
{
    private readonly IWritableOptionsConfigRegistry<T> _optionsRegistry;

    private readonly Dictionary<string, T> _cache = [];
    private readonly Dictionary<string, T> _defaultValue = [];
    private readonly Dictionary<string, List<Action<T, string?>>> _listeners = [];
    private readonly Dictionary<string, FileSystemWatcher?> _watchers = [];
    private readonly Dictionary<string, Timer?> _throttleTimers = [];
    private readonly object _throttleTimersLock = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public OptionsMonitorImpl(IWritableOptionsConfigRegistry<T> optionsRegistry)
    {
        _optionsRegistry = optionsRegistry;
        // subscribe to options added/removed events
        _optionsRegistry.OnAdded += OnOptionsAdded;
        _optionsRegistry.OnRemoved += OnOptionsRemoved;

        // Initialize cache and file watchers
        foreach (var instName in optionsRegistry.GetInstanceNames())
        {
            InitializeOptions(instName);
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
        // remove all options
        foreach (var instanceName in _optionsRegistry.GetInstanceNames())
        {
            OnOptionsRemoved(instanceName);
        }
    }

    /// <summary>
    /// Gets all instance names for which options are configured. <br/>
    /// For use by <see cref="IOptionsSnapshot{TOptions}"/> implementation.
    /// </summary>
    internal IEnumerable<string> GetInstanceNames() => _optionsRegistry.GetInstanceNames();

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
    /// Notification is handled by FileSystemWatcher, not by this method.
    /// </summary>
    /// <param name="instanceName">The name of the instance to update.</param>
    /// <param name="value">The new value to cache.</param>
    internal void UpdateCache(string instanceName, T value)
    {
        _cache[instanceName] = value;
    }

    /// <summary>
    /// Clears the cached value for the specified instance name.
    /// </summary>
    internal void ClearCache(string instanceName)
    {
        _cache.Remove(instanceName);
    }

    // Called when new options are added to the registry.
    private void OnOptionsAdded(WritableOptionsConfiguration<T> options) =>
        InitializeOptions(options.InstanceName);

    // Called when options are removed from the registry.
    private void OnOptionsRemoved(string instanceName)
    {
        _cache.Remove(instanceName);
        _defaultValue.Remove(instanceName);
        _listeners.Remove(instanceName);
        if (_watchers.TryGetValue(instanceName, out var watcher))
        {
            watcher?.Dispose();
            _watchers.Remove(instanceName);
        }
        lock (_throttleTimersLock)
        {
            if (_throttleTimers.TryGetValue(instanceName, out var timer))
            {
                timer?.Dispose();
                _throttleTimers.Remove(instanceName);
            }
        }
    }

    // Initializes options for a given instance name.
    private void InitializeOptions(string instanceName)
    {
        var opt = _optionsRegistry.Get(instanceName);
        // Store the default value for IOptions
        var defaultValue = LoadConfiguration(instanceName);
        _defaultValue[instanceName] = defaultValue;
        // Setup file watcher
        SetupFileWatcher(opt);
    }

    // Loads configuration from the provider and updates the cache.
    private T LoadConfiguration(string instanceName)
    {
        var options = _optionsRegistry.Get(instanceName);
        _semaphore.Wait();
        try
        {
            // Use the provider to load configuration (provider will check file existence via its FileProvider)
            var value = options.FormatProvider.LoadConfiguration<T>(options);
            // Don't notify listeners during initial load, only file change events should notify
            _cache[instanceName] = value;
            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Sets up a FileSystemWatcher to monitor changes to the configuration file.
    private void SetupFileWatcher(WritableOptionsConfiguration<T> options)
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
        var options = _optionsRegistry.Get(instanceName);
        if (options.ConfigFilePath != args.FullPath)
        {
            // Ignore changes to other files in the same directory
            // e.g. temporary file (foobar.json~ABCDEF.TMP)
            return;
        }

        var fileName = Path.GetFileName(options.ConfigFilePath);

        // if enabled throttle, check current status
        if (
            options.OnChangeThrottleMs > 0
            && HandleThrottle(instanceName, options.OnChangeThrottleMs)
        )
        {
            // Still in throttle period, ignore this change
            options.Logger?.LogDebug(
                "Configuration file change detected but ignored due to throttle: {FileName} ({ChangeType})",
                fileName,
                args.ChangeType
            );
            return;
        }

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

    // Checks if the throttle timer is active for the given instance name
    private bool HandleThrottle(string instanceName, int throttleMs)
    {
        lock (_throttleTimersLock)
        {
            if (_throttleTimers.TryGetValue(instanceName, out var timer) && timer != null)
            {
                // Timer is active, so we are in throttle period
                return true;
            }
            // Set a timer that will disable itself after the specified time has elapsed
            var newTimer = new Timer(
                _ =>
                {
                    // Dispose and remove the timer after throttle period
                    lock (_throttleTimersLock)
                    {
                        if (_throttleTimers.TryGetValue(instanceName, out var t))
                        {
                            t?.Dispose();
                            _throttleTimers.Remove(instanceName);
                        }
                    }
                },
                null,
                throttleMs,
                Timeout.Infinite
            );
            _throttleTimers[instanceName] = newTimer;
            return false;
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
