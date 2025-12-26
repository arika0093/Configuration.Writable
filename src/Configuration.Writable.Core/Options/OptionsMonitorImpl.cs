using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Configuration.Writable.Migration;
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
    private readonly ConcurrentDictionary<string, OptionsMonitorDataSource> _dataSources = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
#if NET9_0_OR_GREATER
    private readonly Lock _throttleTimersLock = new();
#else
    private readonly object _throttleTimersLock = new();
#endif

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
        if (_dataSources.TryGetValue(name, out var dataSource))
        {
            // NOTE: Cloning on every Get() call is necessary to prevent external mutations from affecting the cache.
            // To optimize performance, users should provide an efficient CloneMethod (e.g., using source generators
            // or manual cloning instead of JSON serialization).
            return GetClonedValue(name, dataSource.Cache);
        }
        // Load configuration if not cached
        return LoadConfiguration(name);
    }

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener)
    {
        foreach (var instanceName in GetInstanceNames())
        {
            if (_dataSources.TryGetValue(instanceName, out var dataSource))
            {
                dataSource.Listeners.Add(listener);
            }
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
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return GetClonedValue(instanceName, dataSource.DefaultValue);
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
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            dataSource.Cache = value;
        }
    }

    /// <summary>
    /// Clears the cached value for the specified instance name.
    /// </summary>
    internal void ClearCache(string instanceName) => LoadConfiguration(instanceName);

    /// <summary>
    /// Returns a cloned copy of the given value using the clone strategy defined in the options configuration.
    /// </summary>
    /// <param name="instanceName">The name of the options instance. </param>
    /// <param name="value">The value to clone.</param>
    internal T GetClonedValue(string instanceName, T value)
    {
        var options = _optionsRegistry.Get(instanceName);
        return options.CloneMethod(value);
    }

    // Called when new options are added to the registry.
    private void OnOptionsAdded(WritableOptionsConfiguration<T> options) =>
        InitializeOptions(options.InstanceName);

    // Called when options are removed from the registry.
    private void OnOptionsRemoved(string instanceName)
    {
        if (_dataSources.TryRemove(instanceName, out var dataSource))
        {
            dataSource.Dispose();
        }
    }

    // Initializes options for a given instance name.
    private void InitializeOptions(string instanceName)
    {
        var opt = _optionsRegistry.Get(instanceName);
        // Load the initial value
        var initialValue = LoadConfigurationFromProvider(instanceName);
        var defaultValue = opt.CloneMethod(initialValue);
        // Create data source with initial value as both cache and default
        var dataSource = new OptionsMonitorDataSource(initialValue, defaultValue);
        _dataSources[instanceName] = dataSource;
        // Setup file watcher
        SetupFileWatcher(opt, dataSource);
    }

    // Loads configuration from the provider and updates the cache.
    private T LoadConfiguration(string instanceName)
    {
        var value = LoadConfigurationFromProvider(instanceName);
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            // Don't notify listeners during explicit load, only file change events should notify
            dataSource.Cache = value;
        }
        return value;
    }

    // Loads configuration from the provider without updating cache
    private T LoadConfigurationFromProvider(string instanceName)
    {
        var options = _optionsRegistry.Get(instanceName);
        _semaphore.Wait();
        try
        {
            // Use the provider to load configuration (provider will check file existence via its FileProvider)
            return options.FormatProvider.LoadWithMigration<T>(options);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Sets up a FileSystemWatcher to monitor changes to the configuration file.
    private void SetupFileWatcher(
        WritableOptionsConfiguration<T> options,
        OptionsMonitorDataSource dataSource
    )
    {
        var filePath = options.ConfigFilePath;
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            dataSource.Watcher = null;
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
                dataSource.Watcher = null;
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

            dataSource.Watcher = watcher;
        }
        catch
        {
            // If file watching is not supported, continue without it
            dataSource.Watcher = null;
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
            if (_dataSources.TryGetValue(instanceName, out var dataSource))
            {
                // Force reload on next access by loading from file
                try
                {
                    dataSource.Cache = LoadConfigurationFromProvider(instanceName);
                }
                catch
                {
                    // If reload fails, keep the old cached value
                }
            }
        }
    }

    // Checks if the throttle timer is active for the given instance name
    private bool HandleThrottle(string instanceName, int throttleMs)
    {
        if (!_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return false;
        }

        lock (_throttleTimersLock)
        {
            if (dataSource.ThrottleTimer != null)
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
                        if (
                            _dataSources.TryGetValue(instanceName, out var ds)
                            && ds.ThrottleTimer != null
                        )
                        {
                            ds.ThrottleTimer.Dispose();
                            ds.ThrottleTimer = null;
                        }
                    }
                },
                null,
                throttleMs,
                Timeout.Infinite
            );
            dataSource.ThrottleTimer = newTimer;
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
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            foreach (var listener in dataSource.Listeners)
            {
                listener(value, instanceName);
            }
        }
    }

    // Disposable to unregister a listener
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
                foreach (var dataSource in _monitor._dataSources.Values)
                {
                    dataSource.Listeners.Remove(_listener);
                }
            }
            finally
            {
                _monitor._semaphore.Release();
            }

            _disposed = true;
        }
    }

    // Data container for each monitored options instance
    private sealed class OptionsMonitorDataSource : IDisposable
    {
        public T Cache { get; set; }
        public T DefaultValue { get; set; }
        public List<Action<T, string?>> Listeners { get; } = [];
        public FileSystemWatcher? Watcher { get; set; }
        public Timer? ThrottleTimer { get; set; }

        public OptionsMonitorDataSource(T cache, T defaultValue)
        {
            Cache = cache;
            DefaultValue = defaultValue;
        }

        public void Dispose()
        {
            Watcher?.Dispose();
            ThrottleTimer?.Dispose();
        }
    }
}
