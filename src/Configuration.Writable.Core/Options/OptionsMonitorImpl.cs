using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Diagnostics;
using Configuration.Writable.State;
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
    /// Notification is handled by the configured state watcher, not by this method.
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
        StartStateWatcher(opt, dataSource);
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
            var result = new LegacyFileStateSource<T>(options)
                .ReadAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (result.Status != StateReadStatus.Success || result.Value is null)
            {
                throw new InvalidOperationException(
                    $"State source did not return a value for options instance '{instanceName}'."
                );
            }
            return new LoadedConfiguration(
                result.Value,
                ConfigurationFileFingerprint.Capture(options)
            );
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void StartStateWatcher(
        WritableOptionsConfiguration<T> options,
        OptionsMonitorDataSource dataSource
    )
    {
        dataSource.WatcherCancellation?.Cancel();
        dataSource.WatcherCancellation?.Dispose();
        dataSource.WatcherCancellation = new CancellationTokenSource();
        dataSource.WatcherTask = WatchStateChangesAsync(
            options,
            dataSource,
            dataSource.WatcherCancellation.Token
        );
    }

    private async Task WatchStateChangesAsync(
        WritableOptionsConfiguration<T> options,
        OptionsMonitorDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var source = new LegacyFileStateSource<T>(options);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await source
                    .WaitForChangeAsync(dataSource.Fingerprint?.ToRevision(), cancellationToken)
                    .ConfigureAwait(false);
                if (options.OnChangeDebounce > TimeSpan.Zero)
                {
                    await Task.Delay(options.OnChangeDebounce, cancellationToken)
                        .ConfigureAwait(false);
                }

                ReloadAndNotify(options.InstanceName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                HandleReloadFailure(options.InstanceName, exception);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
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
        public CancellationTokenSource? WatcherCancellation { get; set; }
        public Task? WatcherTask { get; set; }

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
            WatcherCancellation?.Cancel();
            WatcherCancellation?.Dispose();
        }
    }

    private sealed class LoadedConfiguration(T value, ConfigurationFileFingerprint? fingerprint)
    {
        internal T Value { get; } = value;
        internal ConfigurationFileFingerprint? Fingerprint { get; } = fingerprint;
    }
}
