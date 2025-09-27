using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
public class WritableConfigJsonProvider : WritableConfigProviderBase
{
    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } =
        new() { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles };

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "json";

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddJsonFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public override void AddConfigurationFile(IConfigurationBuilder configuration, Stream stream) =>
        configuration.AddJsonStream(stream);

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var sectionName = options.SectionName;
        var serializerOptions = JsonSerializerOptions;

        // generate saved json object
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, serializerOptions);
        JsonObject root;

        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            // Use the new nested section creation method
            var nestedSection = CreateNestedSection(sectionName, serializeNode ?? new JsonObject());
            root =
                JsonSerializer.SerializeToNode(nestedSection, serializerOptions) as JsonObject
                ?? new JsonObject();
        }
        else
        {
            // if section name is empty, write the config as root
            root = serializeNode as JsonObject ?? new JsonObject();
        }
        // convert to string
        var jsonString = root?.ToJsonString(serializerOptions) ?? "{}";
        return Encoding.GetBytes(jsonString);
    }
}
