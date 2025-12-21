using System.Collections.Generic;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Options;

/// <summary>
/// Provides a snapshot of options of type <typeparamref name="T"/> that can be read and written during the
/// application's lifetime.
/// </summary>
internal class OptionsSnapshotImpl<T> : IOptionsSnapshot<T>
    where T : class, new()
{
    private readonly OptionsMonitorImpl<T> _optionsMonitor;
    private readonly Dictionary<string, T> _snapshotValues = [];

    public OptionsSnapshotImpl(OptionsMonitorImpl<T> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
        var keys = optionsMonitor.GetInstanceNames();
        foreach (var key in keys)
        {
            _snapshotValues[key] = optionsMonitor.GetDefaultValue(key);
        }
    }

    /// <inheritdoc />
    public T Value => GetCachedDefaultValue(MEOptions.DefaultName);

    /// <inheritdoc />
    public T Get(string? name) => GetCachedDefaultValue(name!);

    // Get the cached default value for the given name
    private T GetCachedDefaultValue(string name) =>
        _optionsMonitor.GetClonedValue(name, _snapshotValues[name]);
}
