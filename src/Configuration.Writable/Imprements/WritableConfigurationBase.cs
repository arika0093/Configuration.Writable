using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Imprements;

/// <summary>
/// Base class for writable configuration implementations.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
internal abstract class WritableConfigurationBase<T>(IOptionsMonitor<T> optionMonitorInstance)
    : IWritableOptions<T>
    where T : class
{
    public abstract Task SaveAsync(T newConfig, CancellationToken cancellationToken = default);

    // -------
    // IOptions<T> implementation
    public T Value => optionMonitorInstance.CurrentValue;

    // -------
    // IOptionsMonitor<T> implementation
    public T CurrentValue => optionMonitorInstance.CurrentValue;

    public T Get(string? name) => optionMonitorInstance.Get(name);

    public IDisposable? OnChange(Action<T, string?> listener) =>
        optionMonitorInstance.OnChange(listener);

    // -------
    // IWritableOptions<T> implementation
    public Task SaveAsync(Action<T> configUpdator, CancellationToken cancellationToken = default)
    {
        var current = optionMonitorInstance.CurrentValue;
        configUpdator(current);
        return SaveAsync(current);
    }
}
