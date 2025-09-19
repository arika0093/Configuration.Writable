using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Provider;

/// <summary>
/// Base class for writable configuration implementations.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <param name="optionMonitorInstance">The options monitor instance.</param>
/// <param name="instanceName">The name of the configuration instance.</param>
public abstract class WritableConfigurationBase<T> : IWritableOptions<T>
    where T : class
{
    private readonly IOptionsMonitor<T> _optionMonitorInstance;
    private readonly IEnumerable<WritableConfigurationOptions<T>> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableConfigurationBase{T}"/> class with the specified options
    /// monitor.
    /// </summary>
    /// <param name="optionMonitorInstance">An <see cref="IOptionsMonitor{T}"/> instance used to monitor and retrieve configuration values.</param>
    /// <param name="configOptions">A collection of <see cref="WritableConfigurationOptions{T}"/> instances. </param>
    protected WritableConfigurationBase(
        IOptionsMonitor<T> optionMonitorInstance,
        IEnumerable<WritableConfigurationOptions<T>> configOptions
    )
    {
        _optionMonitorInstance = optionMonitorInstance;
        _options = configOptions;

        // clear cache on receiving change notification
        _optionMonitorInstance.OnChange(
            (updatedValue, name) =>
            {
                CachedValue.Remove(name!);
            }
        );
    }

    /// <inheritdoc />
    public abstract Task SaveAsync(
        T newConfig,
        string name,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveAsync(newConfig, Options.DefaultName, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(
        Action<T> configUpdator,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var current = CurrentValue;
        configUpdator(current);
        return SaveAsync(current, name, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdator, CancellationToken cancellationToken = default) =>
        SaveAsync(configUpdator, Options.DefaultName, cancellationToken);

    /// <inheritdoc />
    public T CurrentValue =>
        CachedValue.GetValueOrDefault(Options.DefaultName) ?? _optionMonitorInstance.CurrentValue;

    /// <inheritdoc />
    public T Get(string? name) =>
        CachedValue.GetValueOrDefault(name!) ?? _optionMonitorInstance.Get(name);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        _optionMonitorInstance.OnChange(listener);

    /// <summary>
    /// A property to cache values in case <see cref="IOptionsMonitor{T}"/> does not work properly in some environments (docker, network shares, etc.)
    /// </summary>
    private Dictionary<string, T> CachedValue { get; set; } = [];

    /// <summary>
    /// Retrieves a writable configuration option of the specified type and name.
    /// </summary>
    /// <param name="name">The name of the configuration option to retrieve. This value is case-sensitive.</param>
    /// <returns>The <see cref="WritableConfigurationOptions{T}"/> instance that matches the specified name.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple configuration options with the specified name are found, or if no configuration option with
    /// the specified name exists.</exception>
    protected WritableConfigurationOptions<T> GetOption(string name)
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
    protected void SetCachedValue(string instanceName, T value)
    {
        CachedValue[instanceName] = value;
    }

    /// <summary>
    /// Asynchronously saves the specified content to a file at the given path, creating any necessary directories.
    /// </summary>
    /// <param name="path">The full file path where the content will be saved. The directory structure will be created if it does not
    /// exist.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <param name="contentValue"></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    protected static Task SaveToFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken
    )
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
#if NET
        return File.WriteAllTextAsync(path, content, cancellationToken);
#else
        return Task.Run(() => File.WriteAllText(path, content), cancellationToken);
#endif
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
