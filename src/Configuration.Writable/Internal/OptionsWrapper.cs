using Microsoft.Extensions.Options;

namespace Configuration.Writable.Internal;

/// <summary>
/// Implementation of IOptions that wraps IOptionsMonitor and always returns the current value.
/// </summary>
internal sealed class DynamicOptionsWrapper<T> : IOptions<T>
    where T : class
{
    private readonly IOptionsMonitor<T> _monitor;

    public DynamicOptionsWrapper(IOptionsMonitor<T> monitor)
    {
        _monitor = monitor;
    }

    public T Value => _monitor.CurrentValue;
}
