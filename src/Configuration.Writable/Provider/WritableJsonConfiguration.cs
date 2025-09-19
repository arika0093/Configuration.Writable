using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableJsonConfiguration<T> : WritableConfigurationBase<T>
    where T : class
{
    private readonly WritableConfigurationOptions<T> _options;
    private readonly WritableJsonConfigurationOptions<T> _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="WritableJsonConfiguration{T}"/> class,  providing functionality to
    /// manage writable JSON-based configuration for the specified options type.
    /// </summary>
    /// <param name="optionMonitorInstance">An <see cref="IOptionsMonitor{T}"/> instance used to retrieve and monitor changes to the options of type
    /// <typeparamref name="T"/>.</param>
    /// <param name="configOptions">A <see cref="WritableConfigurationOptions{T}"/> instance that specifies the configuration options,  including
    /// the instance name and file path for the writable JSON configuration.</param>
    /// <param name="jsonOptions">Optional. A <see cref="WritableJsonConfigurationOptions{T}"/> instance that specifies the serializer options
    /// for handling JSON serialization and deserialization. If not provided, default options are used.</param>
    public WritableJsonConfiguration(
        IOptionsMonitor<T> optionMonitorInstance,
        WritableConfigurationOptions<T> configOptions,
        WritableJsonConfigurationOptions<T>? jsonOptions = null
    )
        : base(optionMonitorInstance, configOptions.InstanceName)
    {
        _options = configOptions;
        _jsonOptions = jsonOptions ?? new();
    }

    /// <inheritdoc />
    public override Task SaveAsync(T newConfig, CancellationToken cancellationToken = default)
    {
        // naive implementation
        var path = _options.ConfigFilePath;
        var json = JsonSerializer.Serialize<T>(newConfig, _jsonOptions.JsonSerializerOptions);
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
public class WritableJsonConfigurationOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new() { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles };
}
