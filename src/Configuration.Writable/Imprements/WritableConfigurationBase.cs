using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Imprements;

internal abstract class WritableConfigurationBase<T>(IOptionsMonitor<T> optionMonitorInstance)
    : IWritableOptions<T>
    where T : class
{
    public abstract void Save(T newConfig);
    public abstract Task SaveAsync(T newConfig);

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
    public void Save(Action<T> configUpdator)
    {
        var current = optionMonitorInstance.CurrentValue;
        configUpdator(current);
        Save(current);
    }

    public void Save(Func<T, T> configGenerator)
    {
        var current = optionMonitorInstance.CurrentValue;
        var newConfig = configGenerator(current);
        Save(newConfig);
    }

    public Task SaveAsync(Action<T> configUpdator)
    {
        var current = optionMonitorInstance.CurrentValue;
        configUpdator(current);
        return SaveAsync(current);
    }

    public Task SaveAsync(Func<T, T> configGenerator)
    {
        var current = optionMonitorInstance.CurrentValue;
        var newConfig = configGenerator(current);
        return SaveAsync(newConfig);
    }
}
