using System;
using System.Collections.Generic;
using Configuration.Writable.Configure;
using Configuration.Writable.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Testing;

/// <summary>
/// Provides methods to initialize and retrieve writable configuration instances for a specified options type.
/// </summary>
public class WritableOptionsSimpleInstance<T>
    where T : class, new()
{
    private WritableOptionsConfiguration<T>? _options = null;

    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public void Initialize() => Initialize(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(Action<WritableOptionsConfigBuilder<T>> configurationOptions) => 
        Initialize(MEOptions.DefaultName, configurationOptions);

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="instanceName">The name of the options instance.</param>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(
        string instanceName,
        Action<WritableOptionsConfigBuilder<T>> configurationOptions
    )
    {
        var optionBuilder = new WritableOptionsConfigBuilder<T>();
        configurationOptions(optionBuilder);
        optionBuilder.InstanceName = instanceName;
        _options = optionBuilder.BuildOptions();
    }

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public IWritableOptions<T> GetOptions()
    {
        if (_options == null)
        {
            throw new InvalidOperationException(
                "WritableOptionsSimpleInstance is not initialized. Call Initialize() before GetOptions()."
            );
        }
        var options = new List<WritableOptionsConfiguration<T>> { _options };
        var optionsRegistry = new WritableOptionsConfigRegistryImpl<T>(options);
        var optionsMonitor = new OptionsMonitorImpl<T>(optionsRegistry);
        var writableOptions = new WritableOptionsImpl<T>(optionsMonitor, optionsRegistry);
        return writableOptions;
    }
}
