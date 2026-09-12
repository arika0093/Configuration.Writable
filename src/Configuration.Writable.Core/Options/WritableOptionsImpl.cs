using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.Diagnostics;
using Configuration.Writable.Options;
using Configuration.Writable.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable;

/// <summary>
/// Base class for writable configuration implementations.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="WritableOptionsImpl{T}"/> class with the specified options
/// monitor.
/// </remarks>
/// <param name="optionMonitorInstance">An <see cref="OptionsMonitorImpl{T}"/> instance used to monitor and retrieve configuration values.</param>
/// <param name="registryInstance">The configuration options registry instance.</param>
internal sealed class WritableOptionsImpl<T>(
    OptionsMonitorImpl<T> optionMonitorInstance,
    IWritableOptionsConfigRegistry<T> registryInstance
) : IWritableOptionsMonitor<T>, IOptionsMonitor<T>
    where T : class, new()
{
    /// <inheritdoc />
    public IOptionsConfigurationInfo ConfigurationInfo =>
        GetConfigurationInfo(MEOptions.DefaultName);

    internal IOptionsConfigurationInfo GetConfigurationInfo(string name) =>
        OptionsConfigurationInfo.From(GetOptions(name));

    /// <inheritdoc />
    public ConfigureSession<T> BeginConfigure() => BeginConfigure(MEOptions.DefaultName);

    /// <inheritdoc />
    public ConfigureSession<T> BeginConfigure(string name)
    {
        var options = GetOptions(name);
        return new ConfigureSession<T>(
            optionMonitorInstance.Get(name),
            new T(),
            options.CloneMethod,
            (value, cancellationToken) => SaveAsync(name, value, cancellationToken)
        );
    }

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveAsync(MEOptions.DefaultName, newConfig, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        SaveAsync(MEOptions.DefaultName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(string name, T newConfig, CancellationToken cancellationToken = default)
    {
        var options = GetOptions(name);
        var configToSave = options.CloneMethod(newConfig);
        return SaveClonedAsync(configToSave, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        var options = GetOptions(name);
        await UpdateAndSaveAsync(options, configUpdater, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SaveAsync(
        Func<T, Task> configUpdater,
        CancellationToken cancellationToken = default
    ) => SaveAsync(MEOptions.DefaultName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public async Task SaveAsync(
        string name,
        Func<T, Task> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        var options = GetOptions(name);
        await UpdateAndSaveAsync(options, configUpdater, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public T CurrentValue => optionMonitorInstance.CurrentValue;

    /// <inheritdoc />
    public T Get(string name) => optionMonitorInstance.Get(name);

    T IOptionsMonitor<T>.Get(string? name) => optionMonitorInstance.Get(name);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T> listener) =>
        optionMonitorInstance.OnChange(
            (options, instance) =>
            {
                // Invoke listener only for the default instance
                if (instance == MEOptions.DefaultName)
                {
                    listener(options);
                }
            }
        );

    /// <inheritdoc />
    public IDisposable? OnChange(string name, Action<T> listener) =>
        optionMonitorInstance.OnChange(
            (options, instance) =>
            {
                // Invoke listener only for the specified instance
                if (instance == name)
                {
                    listener(options);
                }
            }
        );

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        optionMonitorInstance.OnChange(listener);

    /// <inheritdoc />
    public IDisposable? OnReloadFailed(Action<Exception, string?> listener) =>
        optionMonitorInstance.OnReloadFailed(listener);

    /// <inheritdoc />
    IWritableOptions<T> IWritableNamedOptions<T>.GetInstance(string name) =>
        new WritableOptionsWithNameImpl<T>(this, name);

    /// <inheritdoc />
    IReadOnlyOptions<T> IReadOnlyNamedOptions<T>.GetInstance(string name) =>
        new WritableOptionsWithNameImpl<T>(this, name);

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="options">The writable configuration options associated with the configuration to be saved.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">Thrown when validation fails.</exception>
    private async Task SaveCoreAsync(
        T newConfig,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Validate configuration if a validator is provided
            if (options.Validator != null)
            {
                var validationResult = options.Validator(newConfig);
                if (validationResult.Failed)
                {
                    throw new Microsoft.Extensions.Options.OptionsValidationException(
                        options.InstanceName,
                        typeof(T),
                        validationResult.Failures
                    );
                }
            }

            var expectedRevision =
                options.ConflictResolution == ConfigurationConflictResolution.FailOnConflict
                    ? optionMonitorInstance.GetFingerprint(options.InstanceName)?.ToRevision()
                    : null;
            var writeResult = await new LegacyFileStateSource<T>(options, acquireSaveLock: false)
                .WriteAsync(
                    new StateWriteRequest<T>(newConfig, expectedRevision),
                    cancellationToken
                )
                .ConfigureAwait(false);

            // Update the monitor's cache (FileSystemWatcher will notify listeners)
            var publishedConfig = options.CloneMethod(newConfig);
            optionMonitorInstance.UpdateCache(
                options.InstanceName,
                publishedConfig,
                ConfigurationFileFingerprint.Capture(options)
            );

            options.Logger?.LogInformation(
                "Configuration saved successfully for {InstanceName} at revision {Revision}",
                options.InstanceName,
                writeResult.Revision
            );
            ConfigurationWritableEventSource.Log.SaveSucceeded(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch
        {
            ConfigurationWritableEventSource.Log.SaveFailed();
            throw;
        }
    }

    /// <summary>
    /// Retrieves a writable configuration option of the specified type and name.
    /// </summary>
    /// <param name="name">The name of the configuration option to retrieve. This value is case-sensitive.</param>
    /// <returns>The <see cref="WritableOptionsConfiguration{T}"/> instance that matches the specified name.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple configuration options with the specified name are found, or if no configuration option with
    /// the specified name exists.</exception>
    private WritableOptionsConfiguration<T> GetOptions(string name) => registryInstance.Get(name);

    private async Task SaveClonedAsync(
        T configToSave,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken
    )
    {
        using var fileLock = await AsyncFileSaveLock
            .AcquireAsync(options.ConfigFilePath, cancellationToken)
            .ConfigureAwait(false);
        await SaveCoreAsync(configToSave, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAndSaveAsync(
        WritableOptionsConfiguration<T> options,
        Action<T> configUpdater,
        CancellationToken cancellationToken
    )
    {
        using var fileLock = await AsyncFileSaveLock
            .AcquireAsync(options.ConfigFilePath, cancellationToken)
            .ConfigureAwait(false);
        var configToSave = options.CloneMethod(Get(options.InstanceName));
        configUpdater(configToSave);
        await SaveCoreAsync(configToSave, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAndSaveAsync(
        WritableOptionsConfiguration<T> options,
        Func<T, Task> configUpdater,
        CancellationToken cancellationToken
    )
    {
        using var fileLock = await AsyncFileSaveLock
            .AcquireAsync(options.ConfigFilePath, cancellationToken)
            .ConfigureAwait(false);
        var configToSave = options.CloneMethod(Get(options.InstanceName));
        await configUpdater(configToSave).ConfigureAwait(false);
        await SaveCoreAsync(configToSave, options, cancellationToken).ConfigureAwait(false);
    }
}
