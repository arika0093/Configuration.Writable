using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        options.EffectiveLogger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        var sectionName = options.SectionName;
        var serializerOptions = JsonSerializerOptions;

        // generate saved json object
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, serializerOptions);

        options.EffectiveLogger?.Log(
            LogLevel.Trace,
            "Creating nested section structure for section: {SectionName}",
            sectionName
        );
        // Use the new nested section creation method
        var nestedSection = CreateNestedSection(sectionName, serializeNode ?? new JsonObject());
        var sNode = JsonSerializer.SerializeToNode(nestedSection, serializerOptions);
        JsonObject root = sNode as JsonObject ?? [];
        // convert to string
        var jsonString = root?.ToJsonString(serializerOptions) ?? "{}";
        var bytes = Encoding.GetBytes(jsonString);

        options.EffectiveLogger?.Log(
            LogLevel.Trace,
            "JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );
        return bytes;
    }
}
