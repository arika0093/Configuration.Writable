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
    private readonly Dictionary<string, T> _snapshotValues = [];

    public OptionsSnapshotImpl(OptionsMonitorImpl<T> _optionsMonitor)
    {
        var keys = _optionsMonitor.GetInstanceNames();
        foreach (var key in keys)
        {
            _snapshotValues[key] = _optionsMonitor.GetDefaultValue(key);
        }
    }

    /// <inheritdoc />
    public T Value => _snapshotValues[MEOptions.DefaultName];

    /// <inheritdoc />
    public T Get(string? name) => _snapshotValues[name!];
}
