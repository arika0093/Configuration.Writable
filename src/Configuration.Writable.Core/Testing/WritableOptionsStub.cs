using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Testing;

/// <summary>
/// A simple stub implementation of <see cref="IWritableOptions{T}"/> or <see cref="IReadOnlyOptions{T}"/> for testing purposes.
/// </summary>
/// <typeparam name="T"></typeparam>
public class WritableOptionsStub<T> : IWritableOptionsMonitor<T>
    where T : class, new()
{
    /// <summary>
    /// A dictionary containing named configuration values.
    /// </summary>
    public Dictionary<string, T> NamedValues { get; } = [];

    /// <summary>
    /// A list of change listeners that have been registered.
    /// </summary>
    public List<Action<T, string?>> ChangeListeners { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="value">The initial value to be used for the default name.</param>
    public WritableOptionsStub(T value)
    {
        NamedValues[MEOptions.DefaultName] = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="value">The initial value to be used for the specified name.</param>
    public WritableOptionsStub(string instanceName, T value)
    {
        NamedValues[instanceName] = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="namedValues">A dictionary containing named configuration values.</param>
    public WritableOptionsStub(Dictionary<string, T> namedValues)
    {
        NamedValues = namedValues;
    }

    /// <inheritdoc/>
    public T CurrentValue => NamedValues[MEOptions.DefaultName];

    /// <inheritdoc/>
    public T Get(string? name) => NamedValues[name!];

    /// <inheritdoc/>
    public WritableOptionsConfiguration<T> GetOptionsConfiguration() =>
        GetOptionsConfiguration(MEOptions.DefaultName);

    /// <inheritdoc/>
    public WritableOptionsConfiguration<T> GetOptionsConfiguration(string name)
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
            FormatProvider = null!, // no need for format provider in stub
            CloneMethod = t => t, // no need for cloning in stub
            FileProvider = new CommonFileProvider(),
            OnChangeThrottleMs = 1000,
        };
    }

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T, string?> listener)
    {
        ChangeListeners.Add(listener);
        return new DisposableAction(() => ChangeListeners.Remove(listener));
    }

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T> listener) =>
        OnChange(
            (t, changedName) =>
            {
                if (changedName == MEOptions.DefaultName)
                {
                    listener(t);
                }
            }
        );

    /// <inheritdoc/>
    public IDisposable? OnChange(string name, Action<T> listener) =>
        OnChange(
            (t, changedName) =>
            {
                if (changedName == name)
                {
                    listener(t);
                }
            }
        );

    /// <inheritdoc/>
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        SaveAsync(MEOptions.DefaultName, newConfig, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        SaveAsync(MEOptions.DefaultName, configUpdater, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(string name, T newConfig, CancellationToken cancellationToken = default)
    {
        NamedValues[name] = newConfig;
        foreach (var listener in ChangeListeners.ToList())
        {
            listener(newConfig, name);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveAsync(
        string name,
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        var current = Get(name);
        configUpdater(current);
        NamedValues[name] = current;
        foreach (var listener in ChangeListeners.ToList())
        {
            listener(current, name);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    IWritableOptions<T> IWritableNamedOptions<T>.GetSpecifiedInstance(string name) =>
        new WritableOptionsStubWithName<T>(this, name);

    /// <inheritdoc/>
    IReadOnlyOptions<T> IReadOnlyNamedOptions<T>.GetSpecifiedInstance(string name) =>
        new WritableOptionsStubWithName<T>(this, name);

    // A simple disposable action implementation
    private sealed class DisposableAction(Action disposeAction) : IDisposable
    {
        public void Dispose() => disposeAction();
    }
}

/// <summary>
/// A stub implementation of <see cref="IWritableOptions{T}"/> that is bound to a specific instance name.
/// </summary>
/// <typeparam name="T">The type of the configuration object.</typeparam>
internal sealed class WritableOptionsStubWithName<T>(
    WritableOptionsStub<T> innerStub,
    string instanceName
) : IWritableOptions<T>
    where T : class, new()
{
    /// <inheritdoc/>
    public T CurrentValue => innerStub.Get(instanceName);

    /// <inheritdoc/>
    public WritableOptionsConfiguration<T> GetOptionsConfiguration() =>
        innerStub.GetOptionsConfiguration(instanceName);

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T, string?> listener) =>
        innerStub.OnChange(
            (value, name) =>
            {
                if (name == instanceName)
                {
                    listener(value, name);
                }
            }
        );

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<T> listener) => innerStub.OnChange(instanceName, listener);

    /// <inheritdoc/>
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        innerStub.SaveAsync(instanceName, newConfig, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        innerStub.SaveAsync(instanceName, configUpdater, cancellationToken);
}

/// <summary>
/// A static factory class for creating instances of <see cref="WritableOptionsStub{T}"/>.
/// </summary>
public static class WritableOptionsStub
{
    /// <summary>
    /// Creates a new instance of <see cref="WritableOptionsStub{T}"/> with the specified initial value.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    /// <param name="value">The initial value to be used for the default name.</param>
    /// <returns>A new instance of <see cref="WritableOptionsStub{T}"/>.</returns>
    public static WritableOptionsStub<T> Create<T>(T value)
        where T : class, new() => new(value);

    /// <summary>
    /// Creates a new instance of <see cref="WritableOptionsStub{T}"/> with the specified initial value.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="value">The initial value to be used for the specified instance name.</param>
    /// <returns>A new instance of <see cref="WritableOptionsStub{T}"/>.</returns>
    public static WritableOptionsStub<T> Create<T>(string instanceName, T value)
        where T : class, new() => new(instanceName, value);

    /// <summary>
    /// Creates a new instance of <see cref="WritableOptionsStub{T}"/> with the specified named values.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object.</typeparam>
    /// <param name="namedValues">A dictionary containing named configuration values.</param>
    /// <returns>A new instance of <see cref="WritableOptionsStub{T}"/>.</returns>
    public static WritableOptionsStub<T> Create<T>(Dictionary<string, T> namedValues)
        where T : class, new() => new(namedValues);
}
