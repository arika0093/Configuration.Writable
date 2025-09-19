using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
/// <param name="optionMonitorInstance">An <see cref="IOptionsMonitor{T}"/> instance used to retrieve and monitor changes to the options of type
/// <typeparamref name="T"/>.</param>
/// <param name="configOptions">A <see cref="WritableConfigurationOptions{T}"/> instance that specifies the configuration options,  including
/// the instance name and file path for the writable JSON configuration.</param>
public class WritableJsonConfiguration<T>(
    IOptionsMonitor<T> optionMonitorInstance,
    IEnumerable<WritableConfigurationOptions<T>> configOptions
) : WritableConfigurationBase<T>(optionMonitorInstance, configOptions)
    where T : class
{
    private readonly WritableJsonConfigurationOptions<T> _jsonOptions = new();

    /// <inheritdoc />
    public override Task SaveAsync(
        T newConfig,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var option = GetOption(name);
        var path = option.ConfigFilePath;
        var sectionName = option.SectionName;
        var serializerOptions = _jsonOptions.JsonSerializerOptions;

        // generate saved json object
        var root = new JsonObject();
        var serializeNode = JsonSerializer.SerializeToNode<T>(newConfig, serializerOptions);
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            // if section name is specified, wrap the config in a section
            root[sectionName] = serializeNode;
        }
        else
        {
            // if section name is empty, write the config as root
            root = serializeNode as JsonObject;
        }
        // save to cache
        SetCachedValue(name, newConfig);
        // convert to string
        var jsonString = root?.ToJsonString(serializerOptions) ?? "{}";
        return SaveToFileAsync(path, jsonString, cancellationToken);
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
