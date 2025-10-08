using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.Testing;

/// <summary>
/// A simple stub implementation of <see cref="IWritableOptions{T}"/> for testing purposes.
/// </summary>
/// <typeparam name="T"></typeparam>
public class WritableOptionsStub<T> : IWritableOptions<T>
    where T : class
{
    private const string DefaultName = "";

    private Dictionary<string?, T> NamedValues { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="value">The initial value to be used for the default name.</param>
    public WritableOptionsStub(T value)
    {
        NamedValues[DefaultName] = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="namedValues">A dictionary containing named configuration values.</param>
    public WritableOptionsStub(Dictionary<string?, T> namedValues)
    {
        NamedValues = namedValues;
    }

    /// <inheritdoc/>
    public T CurrentValue => NamedValues[DefaultName];

    /// <inheritdoc/>
    public T Get(string? name) => NamedValues[name];

    /// <inheritdoc/>
    public WritableConfigurationOptions<T> GetConfigurationOptions() =>
        GetConfigurationOptions(DefaultName);

    /// <inheritdoc/>
    public WritableConfigurationOptions<T> GetConfigurationOptions(string name)
    {
        // return dummy options
        var sectionName = $"{typeof(T).Name}";
        if (!string.IsNullOrWhiteSpace(name))
        {
            sectionName = $"{sectionName}-{name}";
        }
        return new()
        {
            ConfigFilePath = "",
            InstanceName = name,
            SectionName = sectionName,
            Provider = null!,
        };
    }

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T, string?> listener) => null;

    /// <inheritdoc/>
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveAsync(DefaultName, newConfig, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(string name, T newConfig, CancellationToken cancellationToken = default)
    {
        NamedValues[name] = newConfig;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveAsync(Action<T> configUpdator, CancellationToken cancellationToken = default) =>
        SaveAsync(DefaultName, configUpdator, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(
        string name,
        Action<T> configUpdator,
        CancellationToken cancellationToken = default
    )
    {
        var current = Get(name);
        configUpdator(current);
        NamedValues[name] = current;
        return Task.CompletedTask;
    }
}
