using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

/// <summary>
/// Implementation of WritableOptions with a specified standard name. <br/>
/// This is a wrapper to allow access as <c>[FromKeyedService("instanceName"))] IWritableOptions&lt;T&gt; ...</c>.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <param name="innerWritableOptionsInstance">The inner writable options instance.</param>
/// <param name="instanceName">The name of the instance.</param>
internal sealed class WritableOptionsWithNameImpl<T>(
    WritableOptionsImpl<T> innerWritableOptionsInstance,
    string instanceName
) : IWritableOptions<T>, IDisposable
    where T : class, new()
{
    /// <inheritdoc />
    public T CurrentValue => innerWritableOptionsInstance.Get(instanceName);

    /// <inheritdoc />
    public void Dispose() => innerWritableOptionsInstance.Dispose();

    /// <inheritdoc />
    public WritableConfigurationOptions<T> GetConfigurationOptions() =>
        innerWritableOptionsInstance.GetConfigurationOptions(instanceName);

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) =>
        innerWritableOptionsInstance.OnChange(listener);

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        innerWritableOptionsInstance.SaveAsync(instanceName, newConfig, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(Action<T> configUpdater, CancellationToken cancellationToken = default) =>
        innerWritableOptionsInstance.SaveAsync(instanceName, configUpdater, cancellationToken);
}
