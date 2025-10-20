using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Configuration.Writable.Options;

internal class ConfigurationOptionsRegistryImpl<T>(
    IEnumerable<WritableConfigurationOptions<T>> options
) : IConfigurationOptionsRegistry<T>
    where T : class, new()
{
    // Map of instance names to their corresponding writable configuration options
    private readonly Dictionary<string, WritableConfigurationOptions<T>> _optionsMap =
        options.ToDictionary(opt => opt.InstanceName);

    /// <inheritdoc />
    public event Action<WritableConfigurationOptions<T>> OnAdded = delegate { };

    /// <inheritdoc />
    public event Action<string> OnRemoved = delegate { };

    /// <inheritdoc />
    public WritableConfigurationOptions<T> Get(string instanceName)
    {
        if (_optionsMap.TryGetValue(instanceName, out var opt))
        {
            return opt;
        }
        throw new KeyNotFoundException($"No configuration registered for instance: {instanceName}");
    }

    /// <inheritdoc />
    public IEnumerable<string> GetInstanceNames() => _optionsMap.Keys;

    /// <inheritdoc />
    public bool TryAdd(Action<WritableConfigurationOptionsBuilder<T>> configure)
    {
        var optionsBuilder = new WritableConfigurationOptionsBuilder<T>();
        configure(optionsBuilder);
        var option = optionsBuilder.BuildOptions();
#if NET
        var rst = _optionsMap.TryAdd(option.InstanceName, option);
#else
        if (_optionsMap.ContainsKey(option.InstanceName))
        {
            // already exists
            return false;
        }
        _optionsMap[option.InstanceName] = option;
        var rst = true;
#endif
        if (rst)
        {
            OnAdded(option);
        }
        return rst;
    }

    /// <inheritdoc />
    public bool TryRemove(string instanceName)
    {
        if (_optionsMap.Remove(instanceName))
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
