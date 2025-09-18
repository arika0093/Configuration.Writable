using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Imprements;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
internal class WritableJsonConfiguration<T> : WritableConfigurationBase<T>
    where T : class
{
    private readonly IOptionsMonitor<WritableJsonConfigurationOptions<T>> _configOptions;

    public WritableJsonConfiguration(
        IOptionsMonitor<T> optionMonitorInstance,
        IOptionsMonitor<WritableJsonConfigurationOptions<T>> configOptions
    )
        : base(optionMonitorInstance)
    {
        _configOptions = configOptions;
    }

    public override Task SaveAsync(T newConfig, CancellationToken cancellationToken = default)
    {
        // naive implementation
        var config = _configOptions.CurrentValue;
        var path = config.ConfigFilePath;
        var json = JsonSerializer.Serialize<T>(newConfig, config.JsonSerializerOptions);
        // if directory not exist, create it
        var directory = System.IO.Path.GetDirectoryName(path)!;
        System.IO.Directory.CreateDirectory(directory);
#if NET
        return System.IO.File.WriteAllTextAsync(path, json, cancellationToken);
#else
        return Task.Run(() => System.IO.File.WriteAllText(path, json), cancellationToken);
#endif
    }
}

/// <summary>
/// Options for <see cref="WritableJsonConfiguration{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <summary>
/// Options for <see cref="WritableJsonConfiguration{T}"/>.
/// </summary>
internal class WritableJsonConfigurationOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the configuration file path.
    /// </summary>
    public string ConfigFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new() { WriteIndented = true };
}
