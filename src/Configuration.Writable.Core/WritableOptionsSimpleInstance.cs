using System;
using System.Collections.Generic;

namespace Configuration.Writable;

/// <summary>
/// Provides methods to initialize and retrieve writable configuration instances for a specified options type.
/// </summary>
public class WritableOptionsSimpleInstance<T>
    where T : class
{
    private WritableConfigurationOptions<T>? _options = null;

    /// <summary>
    /// Initializes writable configuration with default settings.
    /// </summary>
    public void Initialize() => Initialize(_ => { });

    /// <summary>
    /// Initializes writable configuration with custom options.
    /// </summary>
    /// <param name="configurationOptions">An action to customize the configuration options.</param>
    public void Initialize(Action<WritableConfigurationOptionsBuilder<T>> configurationOptions)
    {
        var optionBuilder = new WritableConfigurationOptionsBuilder<T>();
        configurationOptions(optionBuilder);
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
        var options = new List<WritableConfigurationOptions<T>> { _options };
        var optionsMonitor = new OptionsMonitorImpl<T>(options);
        var writableOptions = new WritableOptionsImpl<T>(optionsMonitor, options);
        return writableOptions;
    }
}
