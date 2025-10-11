using System;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Internal;

/// <summary>
/// Implementation of IOptionsSnapshot that wraps IOptionsMonitor.
/// </summary>
internal sealed class OptionsSnapshot<T> : IOptionsSnapshot<T>
    where T : class
{
    private readonly IOptionsMonitor<T> _monitor;

    public OptionsSnapshot(IOptionsMonitor<T> monitor)
    {
        _monitor = monitor;
    }

    public T Value => _monitor.CurrentValue;

    public T Get(string? name) => _monitor.Get(name);
}
