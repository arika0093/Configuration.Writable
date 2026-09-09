using System;
using System.Collections.Generic;
using Configuration.Writable.Configure;
using Configuration.Writable.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Internal;

/// <summary>
/// Provides methods to initialize and retrieve writable configuration instances for a specified options type.
/// </summary>
internal class WritableOptionsSimpleInstanceCore<T>
    where T : class, new()
{
    private readonly List<WritableOptionsConfiguration<T>> _options = [];

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
        var option = optionBuilder.BuildOptions(instanceName);
        _options.Clear();
        _options.Add(option);
    }

    internal void Initialize(
        string instanceName,
        WritableOptionsConfigBuilder<T> optionBuilder,
        bool replace
    )
    {
        var option = optionBuilder.BuildOptions(instanceName);
        if (replace)
        {
            _options.Clear();
        }
        _options.Add(option);
    }

    /// <summary>
    /// Creates a new instance of the writable configuration for the specified type.
    /// </summary>
    public IWritableOptionsMonitor<T> GetOptions()
    {
        if (_options.Count == 0)
        {
            throw new InvalidOperationException(
                "WritableOptionsSimpleInstance is not initialized. Call Initialize() before GetOptions()."
            );
        }
        var optionsRegistry = new WritableOptionsConfigRegistryImpl<T>(_options);
        var optionsMonitor = new OptionsMonitorImpl<T>(optionsRegistry);
        var writableOptions = new WritableOptionsImpl<T>(optionsMonitor, optionsRegistry);
        return writableOptions;
    }
}
