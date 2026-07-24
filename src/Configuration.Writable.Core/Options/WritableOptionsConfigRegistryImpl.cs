using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Configuration.Writable.Configure;

namespace Configuration.Writable.Options;

internal class WritableOptionsConfigRegistryImpl<T>(
    IEnumerable<WritableOptionsConfiguration<T>> options
) : IWritableOptionsConfigRegistry<T>
    where T : class, new()
{
    // Map of instance names to their corresponding writable configuration options
    // If multiple configurations have the same InstanceName, the last one wins (consistent with DI container behavior)
    private readonly ConcurrentDictionary<string, WritableOptionsConfiguration<T>> _optionsMap =
        new(options.GroupBy(conf => conf.InstanceName).ToDictionary(g => g.Key, g => g.Last()));

    /// <inheritdoc />
    public event Action<WritableOptionsConfiguration<T>> OnAdded = delegate { };

    /// <inheritdoc />
    public event Action<string> OnRemoved = delegate { };

    /// <inheritdoc />
    public WritableOptionsConfiguration<T> Get(string instanceName)
    {
        if (_optionsMap.TryGetValue(instanceName, out var opt))
        {
            return opt;
        }
        throw new KeyNotFoundException($"No configuration registered for instance: {instanceName}");
    }

    /// <inheritdoc />
    public IEnumerable<string> GetInstanceNames() => _optionsMap.Keys.ToArray();

    /// <inheritdoc />
    public bool TryAdd(string instanceName, Action<WritableOptionsConfigBuilder<T>> configure)
    {
        var optionsBuilder = new WritableOptionsConfigBuilder<T>();
        configure(optionsBuilder);
        var option = optionsBuilder.BuildOptions(instanceName);
        return TryAdd(option);
    }

    /// <inheritdoc />
    public bool TryAdd(WritableOptionsConfiguration<T> configuration)
    {
        if (_optionsMap.TryAdd(configuration.InstanceName, configuration))
        {
            OnAdded(configuration);
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public bool TryRemove(string instanceName)
    {
        if (_optionsMap.TryRemove(instanceName, out _))
        {
            OnRemoved(instanceName);
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public void Clear()
    {
        // ToList() to avoid collection modification during enumeration
        foreach (var instanceName in GetInstanceNames().ToList())
        {
            // Reuse TryRemove to ensure OnRemoved event is triggered
            TryRemove(instanceName);
        }
    }
}
