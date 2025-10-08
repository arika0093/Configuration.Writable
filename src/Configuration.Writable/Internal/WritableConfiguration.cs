using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Internal;

/// <summary>
/// Base class for writable configuration implementations.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
internal sealed class WritableConfiguration<T> : IWritableOptions<T>, IDisposable
    where T : class
{
    private readonly IOptionsMonitor<T> _optionMonitorInstance;
    private readonly IEnumerable<WritableConfigurationOptions<T>> _options;
    private readonly IDisposable? _onChangeToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableConfiguration{T}"/> class with the specified options
    /// monitor.
    /// </summary>
    /// <param name="optionMonitorInstance">An <see cref="IOptionsMonitor{T}"/> instance used to monitor and retrieve configuration values.</param>
    /// <param name="configOptions">A collection of <see cref="WritableConfigurationOptions{T}"/> instances. </param>
    public WritableConfiguration(
        IOptionsMonitor<T> optionMonitorInstance,
        IEnumerable<WritableConfigurationOptions<T>> configOptions
    )
    {
        _optionMonitorInstance = optionMonitorInstance;
        _options = configOptions;

        // clear cache on receiving change notification
        _onChangeToken = _optionMonitorInstance.OnChange(
            (updatedValue, name) =>
            {
                CachedValue.Remove(name!);
            }
        );
    }

    /// <inheritdoc />
    public WritableConfigurationOptions<T> GetConfigurationOptions() =>
        GetOption(Options.DefaultName);

    /// <inheritdoc />
    public WritableConfigurationOptions<T> GetConfigurationOptions(string name) => GetOption(name);

    /// <inheritdoc />
    public Task SaveAsync(
        string name,
        T newConfig,
        CancellationToken cancellationToken = default
    ) => SaveCoreAsync(newConfig, GetOption(name), cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(newConfig, GetOption(Options.DefaultName), cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        var current = DeepCopy(CurrentValue);
        configUpdater(current);
        return SaveCoreAsync(current, GetOption(name), cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        SaveAsync(Options.DefaultName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public T CurrentValue =>
        CachedValue.GetValueOrDefault(Options.DefaultName) ?? _optionMonitorInstance.CurrentValue;

    /// <inheritdoc />
    public T Get(string? name) =>
        CachedValue.GetValueOrDefault(name!) ?? _optionMonitorInstance.Get(name);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        _optionMonitorInstance.OnChange(listener);

    /// <inheritdoc />
    public void Dispose() => _onChangeToken?.Dispose();

    /// <summary>
    /// A property to cache values in case <see cref="IOptionsMonitor{T}"/> does not work properly in some environments (docker, network shares, etc.)
    /// </summary>
    private Dictionary<string, T> CachedValue { get; set; } = [];

    /// <summary>
    /// Asynchronously saves the specified configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration to save.</param>
    /// <param name="options">The writable configuration options associated with the configuration to be saved.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    private Task SaveCoreAsync(
        T newConfig,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
    {
        SetCachedValue(options.InstanceName, newConfig);
        return options.Provider.SaveAsync(newConfig, options, cancellationToken);
    }

    /// <summary>
    /// Retrieves a writable configuration option of the specified type and name.
    /// </summary>
    /// <param name="name">The name of the configuration option to retrieve. This value is case-sensitive.</param>
    /// <returns>The <see cref="WritableConfigurationOptions{T}"/> instance that matches the specified name.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple configuration options with the specified name are found, or if no configuration option with
    /// the specified name exists.</exception>
    private WritableConfigurationOptions<T> GetOption(string name)
    {
        var matchedOptions = _options.Where(o => o.InstanceName == name).ToList();
        if (matchedOptions.Count >= 2)
        {
            throw new InvalidOperationException(
                $"Multiple WritableConfigurationOptions<{typeof(T).Name}> found for the specified name: {name}"
            );
        }
        return matchedOptions.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No WritableConfigurationOptions<{typeof(T).Name}> found for the specified name: {name}"
            );
    }

    /// <summary>
    /// Sets the specified value in the cache for the given key.
    /// </summary>
    /// <param name="instanceName">Option's instance name.</param>
    /// <param name="value">Used to cache values in environments where IOptionsMonitor does not work properly.</param>
    private void SetCachedValue(string instanceName, T value)
    {
        CachedValue[instanceName] = value;
    }

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

#if !NET
file static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key
    )
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }
        return default;
    }
}
#endif
