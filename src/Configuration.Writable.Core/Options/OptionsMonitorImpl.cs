using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Configuration.Writable.Diagnostics;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Migration;
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
    private readonly ConcurrentDictionary<string, OptionsMonitorDataSource> _dataSources = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _listenersLock = new();
    private readonly List<Action<T, string?>> _listeners = [];
    private readonly List<Action<Exception, string?>> _failureListeners = [];
    private static readonly TimeSpan MaxWatcherRecoveryDelay = TimeSpan.FromSeconds(30);
#if NET9_0_OR_GREATER
    private readonly Lock _debounceTimersLock = new();
#else
    private readonly object _debounceTimersLock = new();
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
        lock (_listenersLock)
        {
            _listeners.Add(listener);
            foreach (var dataSource in _dataSources.Values)
            {
                dataSource.AddListener(listener);
            }
        }
        return new ChangeTrackerDisposable(this, listener);
    }

    /// <inheritdoc />
    public IDisposable? OnReloadFailed(Action<Exception, string?> listener)
    {
        lock (_listenersLock)
        {
            _failureListeners.Add(listener);
            foreach (var dataSource in _dataSources.Values)
            {
                dataSource.AddFailureListener(listener);
            }
        }
        return new ReloadFailureTrackerDisposable(this, listener);
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
    /// <param name="fingerprint">The fingerprint associated with the cached value.</param>
    internal void UpdateCache(
        string instanceName,
        T value,
        ConfigurationFileFingerprint? fingerprint = null
    )
    {
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            dataSource.Cache = value;
            dataSource.Fingerprint = fingerprint;
        }
    }

    internal ConfigurationFileFingerprint? GetFingerprint(string instanceName) =>
        _dataSources.TryGetValue(instanceName, out var dataSource) ? dataSource.Fingerprint : null;

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
    private void OnOptionsAdded(WritableOptionsConfiguration<T> options)
    {
        InitializeOptions(options.InstanceName);
        if (!_dataSources.TryGetValue(options.InstanceName, out var dataSource))
        {
            return;
        }

        lock (_listenersLock)
        {
            foreach (var listener in _listeners)
            {
                dataSource.AddListener(listener);
            }

            foreach (var listener in _failureListeners)
            {
                dataSource.AddFailureListener(listener);
            }
        }
    }

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
        var initial = LoadConfigurationFromProvider(instanceName);
        var defaultValue = opt.CloneMethod(initial.Value);
        // Create data source with initial value as both cache and default
        var dataSource = new OptionsMonitorDataSource(
            initial.Value,
            defaultValue,
            initial.Fingerprint
        );
        _dataSources[instanceName] = dataSource;
        // Setup file watcher
        SetupFileWatcher(opt, dataSource);
    }

    // Loads configuration from the provider and updates the cache.
    private T LoadConfiguration(string instanceName)
    {
        var loaded = LoadConfigurationFromProvider(instanceName);
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            // Don't notify listeners during explicit load, only file change events should notify
            dataSource.Cache = loaded.Value;
            dataSource.Fingerprint = loaded.Fingerprint;
        }
        return loaded.Value;
    }

    // Loads configuration from the provider without updating cache
    private LoadedConfiguration LoadConfigurationFromProvider(string instanceName)
    {
        var options = _optionsRegistry.Get(instanceName);
        _semaphore.Wait();
        try
        {
            var readOptions = options with { ConfigFilePath = options.ReadFilePath };
            var value = readOptions.FormatProvider.LoadWithMigration<T>(readOptions);
            if (
                options.PromoteSaveLocationEnabled
                && !string.Equals(
                    options.ReadFilePath,
                    options.ConfigFilePath,
                    StringComparison.Ordinal
                )
            )
            {
                options.Logger?.LogInformation(
                    "Promoting configuration from {ReadFilePath} to {ConfigFilePath}",
                    options.ReadFilePath,
                    options.ConfigFilePath
                );
                options.FormatProvider.SaveAsync(value, options).GetAwaiter().GetResult();
            }
            return new LoadedConfiguration(value, ConfigurationFileFingerprint.Capture(options));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Sets up a FileSystemWatcher to monitor changes to the configuration file.
    private bool SetupFileWatcher(
        WritableOptionsConfiguration<T> options,
        OptionsMonitorDataSource dataSource
    )
    {
        var filePath = GetWatchPath(options);
        var directory = Path.GetDirectoryName(filePath);
        var fileName =
            options.FormatProvider is FormatProvider.FallbackFormatProvider
                ? "*"
                : Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            dataSource.Watcher = null;
            return false;
        }

        // Create directory if it doesn't exist
        if (!Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                options.Logger?.LogWarning(
                    ex,
                    "Configuration directory could not be created for watcher: {Directory}",
                    directory
                );
                dataSource.Watcher = null;
                return false;
            }
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            watcher.Changed += (sender, args) =>
                OnFileChanged(options.InstanceName, args, filePath);
            watcher.Created += (sender, args) =>
                OnFileChanged(options.InstanceName, args, filePath);
            watcher.Deleted += (sender, args) =>
                OnFileChanged(options.InstanceName, args, filePath);
            watcher.Renamed += (sender, args) =>
                OnFileChanged(options.InstanceName, args, filePath);
            watcher.Error += (sender, args) => OnWatcherError(options.InstanceName, args);

            dataSource.Watcher = watcher;
            dataSource.WatchedPath = filePath;
            dataSource.WatcherRecoveryAttempts = 0;
            dataSource.WatcherRecoveryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            return true;
        }
        catch (IOException ex)
        {
            options.Logger?.LogWarning(
                ex,
                "Configuration file watcher could not be started: {FileName}",
                fileName
            );
            dataSource.Watcher = null;
            dataSource.WatchedPath = null;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            options.Logger?.LogWarning(
                ex,
                "Configuration file watcher could not be started: {FileName}",
                fileName
            );
            dataSource.Watcher = null;
            dataSource.WatchedPath = null;
            return false;
        }
    }

    // Called when the configuration file changes
    private void OnFileChanged(string instanceName, FileSystemEventArgs args, string watchedPath)
    {
        var options = _optionsRegistry.Get(instanceName);
        if (
            !_dataSources.TryGetValue(instanceName, out var dataSource)
            || !IsRelevantFileChange(options, watchedPath, args.FullPath)
        )
        {
            // Ignore changes to other files in the same directory
            // e.g. temporary file (foobar.json~ABCDEF.TMP)
            return;
        }

        var selectedFilePath = GetSelectedFilePath(options);
        if (!options.FileProvider.FileExists(selectedFilePath))
        {
            var exception = new FileNotFoundException(
                $"Configuration file was deleted: {selectedFilePath}",
                selectedFilePath
            );
            options.Logger?.LogError(exception, "Configuration file was deleted.");
            NotifyReloadFailure(instanceName, exception);
            RebindFileWatcher(options, dataSource);
            return;
        }

        var fileName = Path.GetFileName(options.ConfigFilePath);

        if (options.OnChangeDebounce > TimeSpan.Zero)
        {
            DebounceReload(instanceName, options.OnChangeDebounce);
            options.Logger?.LogDebug(
                "Configuration file change detected and queued for debounce: {FileName} ({ChangeType})",
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

        ReloadAndNotify(instanceName);
        RebindFileWatcher(options, dataSource);
    }

    private static string GetWatchPath(WritableOptionsConfiguration<T> options)
    {
        var selectedFilePath = GetSelectedFilePath(options);
        return options.FileProvider is IPhysicalFileProvider physicalFileProvider
            ? physicalFileProvider.GetPhysicalFilePath(selectedFilePath)
            : selectedFilePath;
    }

    private static string GetSelectedFilePath(WritableOptionsConfiguration<T> options) =>
        options.FormatProvider is FormatProvider.FallbackFormatProvider fallbackProvider
            ? fallbackProvider.GetSelectedFilePath(options)
            : options.ConfigFilePath;

    private static bool IsRelevantFileChange(
        WritableOptionsConfiguration<T> options,
        string watchedPath,
        string changedPath
    )
    {
        var pathComparison = GetPathComparison();
        if (string.Equals(watchedPath, changedPath, pathComparison))
        {
            return true;
        }

        if (options.FormatProvider is not FormatProvider.FallbackFormatProvider)
        {
            return false;
        }

        var canonicalPath = GetPhysicalPath(options, options.ConfigFilePath);
        var selectedPath = GetWatchPath(options);
        if (
            string.Equals(canonicalPath, changedPath, pathComparison)
            || string.Equals(selectedPath, changedPath, pathComparison)
        )
        {
            return true;
        }

        return false;
    }

    private static string GetPhysicalPath(
        WritableOptionsConfiguration<T> options,
        string configFilePath
    ) =>
        options.FileProvider is IPhysicalFileProvider physicalFileProvider
            ? physicalFileProvider.GetPhysicalFilePath(configFilePath)
            : configFilePath;

    private void RebindFileWatcher(
        WritableOptionsConfiguration<T> options,
        OptionsMonitorDataSource dataSource
    )
    {
        var currentWatchPath = GetWatchPath(options);
        if (string.Equals(dataSource.WatchedPath, currentWatchPath, GetPathComparison()))
        {
            return;
        }

        dataSource.Watcher?.Dispose();
        dataSource.Watcher = null;
        SetupFileWatcher(options, dataSource);
    }

    private static StringComparison GetPathComparison() =>
        Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private void OnWatcherError(string instanceName, ErrorEventArgs args)
    {
        var exception = args.GetException();
        var options = _optionsRegistry.Get(instanceName);
        options.Logger?.LogWarning(
            exception,
            "Configuration file watcher failed and will be recreated: {ConfigFilePath}",
            options.ConfigFilePath
        );
        NotifyReloadFailure(instanceName, exception);

        if (!_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return;
        }

        dataSource.Watcher?.Dispose();
        dataSource.Watcher = null;
        if (!SetupFileWatcher(options, dataSource))
        {
            ScheduleWatcherRecovery(instanceName, dataSource);
        }
    }

    private void ScheduleWatcherRecovery(string instanceName, OptionsMonitorDataSource dataSource)
    {
        var attempt = ++dataSource.WatcherRecoveryAttempts;
        var delayMilliseconds = Math.Min(
            1000 * (1 << Math.Min(attempt - 1, 5)),
            (int)MaxWatcherRecoveryDelay.TotalMilliseconds
        );
        dataSource.WatcherRecoveryTimer ??= new Timer(
            _ => RecoverWatcher(instanceName),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );
        dataSource.WatcherRecoveryTimer.Change(delayMilliseconds, Timeout.Infinite);
    }

    private void RecoverWatcher(string instanceName)
    {
        if (!_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return;
        }

        var options = _optionsRegistry.Get(instanceName);
        if (!SetupFileWatcher(options, dataSource))
        {
            ScheduleWatcherRecovery(instanceName, dataSource);
        }
    }

    // Delays reload until changes have stopped for the configured duration.
    private void DebounceReload(string instanceName, TimeSpan debounceDuration)
    {
        if (!_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return;
        }

        lock (_debounceTimersLock)
        {
            if (dataSource.DebounceTimer == null)
            {
                dataSource.DebounceTimer = new Timer(
                    _ => OnDebounceTimerElapsed(instanceName),
                    null,
                    Timeout.Infinite,
                    Timeout.Infinite
                );
                dataSource.HasPendingDebouncedChange = true;
                dataSource.DebounceTimer.Change(debounceDuration, Timeout.InfiniteTimeSpan);
                return;
            }

            dataSource.HasPendingDebouncedChange = true;
            dataSource.DebounceTimer.Change(debounceDuration, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceTimerElapsed(string instanceName)
    {
        if (!_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            return;
        }

        lock (_debounceTimersLock)
        {
            dataSource.DebounceTimer?.Dispose();
            dataSource.DebounceTimer = null;
            if (!dataSource.HasPendingDebouncedChange)
            {
                return;
            }
            dataSource.HasPendingDebouncedChange = false;
        }

        ReloadAndNotify(instanceName);
    }

    // Reloads configuration and notifies listeners with retry logic for file access conflicts.
    private void ReloadAndNotify(string instanceName)
    {
        try
        {
            var newValue = LoadConfigurationWithRetry(instanceName);
            NotifyListeners(instanceName, newValue);
        }
        catch (IOException ex)
        {
            HandleReloadFailure(instanceName, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            HandleReloadFailure(instanceName, ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            HandleReloadFailure(instanceName, ex);
        }
        catch (FormatException ex)
        {
            HandleReloadFailure(instanceName, ex);
        }
    }

    private void HandleReloadFailure(string instanceName, Exception exception)
    {
        ConfigurationWritableEventSource.Log.ReloadFailed();
        var options = _optionsRegistry.Get(instanceName);
        options.Logger?.LogError(
            exception,
            "Configuration reload failed; the last valid value will be retained: {ConfigFilePath}",
            options.ConfigFilePath
        );
        NotifyReloadFailure(instanceName, exception);
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
            foreach (var listener in dataSource.GetListenersSnapshot())
            {
                try
                {
                    listener(value, instanceName);
                }
                catch (Exception ex)
                {
                    var options = _optionsRegistry.Get(instanceName);
                    options.Logger?.LogError(
                        ex,
                        "Configuration change listener failed: {ConfigFilePath}",
                        options.ConfigFilePath
                    );
                }
            }
        }
    }

    private void NotifyReloadFailure(string instanceName, Exception exception)
    {
        if (_dataSources.TryGetValue(instanceName, out var dataSource))
        {
            foreach (var listener in dataSource.GetFailureListenersSnapshot())
            {
                try
                {
                    listener(exception, instanceName);
                }
                catch (Exception listenerException)
                {
                    var options = _optionsRegistry.Get(instanceName);
                    options.Logger?.LogError(
                        listenerException,
                        "Configuration reload failure listener failed: {ConfigFilePath}",
                        options.ConfigFilePath
                    );
                }
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

            lock (_monitor._listenersLock)
            {
                _monitor._listeners.Remove(_listener);
                foreach (var dataSource in _monitor._dataSources.Values)
                {
                    dataSource.RemoveListener(_listener);
                }
            }

            _disposed = true;
        }
    }

    private sealed class ReloadFailureTrackerDisposable : IDisposable
    {
        private readonly OptionsMonitorImpl<T> _monitor;
        private readonly Action<Exception, string?> _listener;
        private bool _disposed;

        public ReloadFailureTrackerDisposable(
            OptionsMonitorImpl<T> monitor,
            Action<Exception, string?> listener
        )
        {
            _monitor = monitor;
            _listener = listener;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_monitor._listenersLock)
            {
                _monitor._failureListeners.Remove(_listener);
                foreach (var dataSource in _monitor._dataSources.Values)
                {
                    dataSource.RemoveFailureListener(_listener);
                }
            }

            _disposed = true;
        }
    }

    // Data container for each monitored options instance
    private sealed class OptionsMonitorDataSource : IDisposable
    {
        public T Cache { get; set; }
        public T DefaultValue { get; set; }
        public ConfigurationFileFingerprint? Fingerprint { get; set; }
        public List<Action<T, string?>> Listeners { get; } = [];
        public List<Action<Exception, string?>> FailureListeners { get; } = [];
        private object ListenersLock { get; } = new();
        public FileSystemWatcher? Watcher { get; set; }
        public string? WatchedPath { get; set; }
        public Timer? DebounceTimer { get; set; }
        public Timer? WatcherRecoveryTimer { get; set; }
        public int WatcherRecoveryAttempts { get; set; }
        public bool HasPendingDebouncedChange { get; set; }

        public OptionsMonitorDataSource(
            T cache,
            T defaultValue,
            ConfigurationFileFingerprint? fingerprint
        )
        {
            Cache = cache;
            DefaultValue = defaultValue;
            Fingerprint = fingerprint;
        }

        public void AddListener(Action<T, string?> listener)
        {
            lock (ListenersLock)
            {
                Listeners.Add(listener);
            }
        }

        public void RemoveListener(Action<T, string?> listener)
        {
            lock (ListenersLock)
            {
                Listeners.Remove(listener);
            }
        }

        public void AddFailureListener(Action<Exception, string?> listener)
        {
            lock (ListenersLock)
            {
                FailureListeners.Add(listener);
            }
        }

        public void RemoveFailureListener(Action<Exception, string?> listener)
        {
            lock (ListenersLock)
            {
                FailureListeners.Remove(listener);
            }
        }

        public Action<T, string?>[] GetListenersSnapshot()
        {
            lock (ListenersLock)
            {
                return [.. Listeners];
            }
        }

        public Action<Exception, string?>[] GetFailureListenersSnapshot()
        {
            lock (ListenersLock)
            {
                return [.. FailureListeners];
            }
        }

        public void Dispose()
        {
            Watcher?.Dispose();
            DebounceTimer?.Dispose();
            WatcherRecoveryTimer?.Dispose();
        }
    }

    private sealed class LoadedConfiguration(T value, ConfigurationFileFingerprint? fingerprint)
    {
        internal T Value { get; } = value;
        internal ConfigurationFileFingerprint? Fingerprint { get; } = fingerprint;
    }
}
