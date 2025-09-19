using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Provider;

/// <summary>
/// Base class for writable configuration implementations.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <param name="optionMonitorInstance">The options monitor instance.</param>
/// <param name="instanceName">The name of the configuration instance.</param>
public abstract class WritableConfigurationBase<T>(
    IOptionsMonitor<T> optionMonitorInstance,
    string instanceName
) : IWritableOptions<T>
    where T : class
{
    /// <inheritdoc />
    public abstract Task SaveAsync(T newConfig, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public T Value => optionMonitorInstance.Get(instanceName);

    /// <inheritdoc />
    public T CurrentValue => optionMonitorInstance.Get(instanceName);

    /// <inheritdoc />
    public T Get(string? name) => optionMonitorInstance.Get(name);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        optionMonitorInstance.OnChange(listener);

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdator, CancellationToken cancellationToken = default)
    {
        var current = Value;
        configUpdator(current);
        return SaveAsync(current, cancellationToken);
    }
}
