using System;
using Configuration.Writable.Configure;

namespace Configuration.Writable.Testing;

/// <summary>
/// Provides methods to initialize and retrieve writable configuration instances for a specified options type.
/// </summary>
public class WritableOptionsSimpleInstance<T>
    where T : class, new()
{
    private readonly Internal.WritableOptionsSimpleInstanceCore<T> _instance = new();

    /// <summary>Initializes writable configuration with default settings.</summary>
    public void Initialize() => _instance.Initialize();

    /// <summary>Initializes writable configuration with custom options.</summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(Action<WritableOptionsConfigBuilder<T>> configurationOptions) =>
        _instance.Initialize(configurationOptions);

    /// <summary>Initializes writable configuration with custom options.</summary>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(
        string instanceName,
        Action<WritableOptionsConfigBuilder<T>> configurationOptions
    ) => _instance.Initialize(instanceName, configurationOptions);

    /// <summary>Creates a new instance of the writable configuration for the specified type.</summary>
    public IWritableOptionsMonitor<T> GetOptions() => _instance.GetOptions();
}
