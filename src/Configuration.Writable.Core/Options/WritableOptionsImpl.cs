using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Options;
using Microsoft.Extensions.Logging;
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
    IConfigurationOptionsRegistry<T> registryInstance
) : IWritableOptions<T>, IDisposable
    where T : class, new()
{
    /// <inheritdoc />
    public WritableConfigurationOptions<T> GetConfigurationOptions() =>
        GetOptions(MEOptions.DefaultName);

    /// <inheritdoc />
    public WritableConfigurationOptions<T> GetConfigurationOptions(string name) => GetOptions(name);

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(newConfig, GetOptions(MEOptions.DefaultName), cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        SaveWithNameAsync(MEOptions.DefaultName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(
        Action<T, IOptionOperator<T>> configUpdaterWithOperator,
        CancellationToken cancellationToken = default
    ) => SaveWithNameAsync(MEOptions.DefaultName, configUpdaterWithOperator, cancellationToken);

    /// <inheritdoc />
    public Task SaveWithNameAsync(
        string name,
        T newConfig,
        CancellationToken cancellationToken = default
    ) => SaveCoreAsync(newConfig, GetOptions(name), cancellationToken);

    /// <inheritdoc />
    public Task SaveWithNameAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        var current = DeepCopy(Get(name));
        configUpdater(current);
        return SaveCoreAsync(current, GetOptions(name), cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveWithNameAsync(
        string name,
        Action<T, IOptionOperator<T>> configUpdaterWithOperator,
        CancellationToken cancellationToken = default
    )
    {
        var current = DeepCopy(Get(name));
        var operations = new OptionOperations<T>();
        configUpdaterWithOperator(current, operations);
        return SaveCoreAsync(current, operations, GetOptions(name), cancellationToken);
    }

    /// <inheritdoc />
    public T CurrentValue => optionMonitorInstance.CurrentValue;

    /// <inheritdoc />
    public T Get(string? name) => optionMonitorInstance.Get(name);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        optionMonitorInstance.OnChange(listener);

    /// <inheritdoc />
    public void Dispose() => optionMonitorInstance?.Dispose();

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="options">The writable configuration options associated with the configuration to be saved.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">Thrown when validation fails.</exception>
    private async Task SaveCoreAsync(
        T newConfig,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
    {
        // Operations with no effect
        await SaveCoreAsync(newConfig, new OptionOperations<T>(), options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves the specified configuration with operations.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="operations">The operations to perform on the configuration.</param>
    /// <param name="options">The writable configuration options associated with the configuration to be saved.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">Thrown when validation fails.</exception>
    private async Task SaveCoreAsync(
        T newConfig,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
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

        options.Logger?.LogDebug("Saving configuration to {FilePath}", options.ConfigFilePath);

        // Save to file with operations
        await options
            .Provider.SaveAsync(newConfig, operations, options, cancellationToken)
            .ConfigureAwait(false);

        // If operations were performed (e.g., key deletion), reload from file to get accurate state
        // Otherwise use the in-memory config for better performance
        T configToCache;
        if (operations.HasOperations)
        {
            options.Logger?.LogDebug(
                "Reloading configuration from {FilePath} after operations",
                options.ConfigFilePath
            );
            configToCache = options.Provider.LoadConfiguration(options);
        }
        else
        {
            configToCache = newConfig;
        }

        // Update the monitor's cache
        optionMonitorInstance.UpdateCache(options.InstanceName, configToCache);

        var fileName = Path.GetFileName(options.ConfigFilePath);
        options.Logger?.LogInformation("Configuration saved successfully to {FileName}", fileName);
    }

    /// <summary>
    /// Retrieves a writable configuration option of the specified type and name.
    /// </summary>
    /// <param name="name">The name of the configuration option to retrieve. This value is case-sensitive.</param>
    /// <returns>The <see cref="WritableConfigurationOptions{T}"/> instance that matches the specified name.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple configuration options with the specified name are found, or if no configuration option with
    /// the specified name exists.</exception>
    private WritableConfigurationOptions<T> GetOptions(string name) => registryInstance.Get(name);

    /// <summary>
    /// Creates a deep copy of the specified object using JSON serialization/deserialization.
    /// </summary>
    /// <param name="original">The original object to copy.</param>
    /// <returns>A deep copy of the original object.</returns>
    private static T DeepCopy(T original)
    {
        var json = JsonSerializer.Serialize(original);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
