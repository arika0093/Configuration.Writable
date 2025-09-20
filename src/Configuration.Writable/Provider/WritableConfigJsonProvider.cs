using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableConfigJsonProvider<T> : IWritableConfigProvider<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } =
        new() { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles };

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

    /// <inheritdoc />
    public string FileExtension => "json";

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddJsonFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents(T config, WritableConfigurationOptions<T> options)
    {
        var sectionName = options.SectionName;
        var serializerOptions = JsonSerializerOptions;

        // generate saved json object
        var root = new JsonObject();
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, serializerOptions);
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
        // convert to string
        var jsonString = root?.ToJsonString(serializerOptions) ?? "{}";
        return Encoding.GetBytes(jsonString);
    }
}
