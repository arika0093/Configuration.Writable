using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    public override T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
        where T : class
    {
        var filePath = options.ConfigFilePath;
        if (!FileWriter.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileWriter.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance<T>();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class
    {
        var jsonDocument = JsonDocument.Parse(stream);
        var root = jsonDocument.RootElement;

        // Navigate to the section if specified
        var sectionName = options.SectionName;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var sections = GetSplitedSections(sectionName);
            var current = root;

            foreach (var section in sections)
            {
                if (current.TryGetProperty(section, out var element))
                {
                    current = element;
                }
                else
                {
                    // Section not found, return default instance
                    return Activator.CreateInstance<T>();
                }
            }

            return JsonSerializer.Deserialize<T>(current.GetRawText(), JsonSerializerOptions)
                ?? Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonSerializerOptions)
            ?? Activator.CreateInstance<T>();
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        var sectionName = options.SectionName;
        var serializerOptions = JsonSerializerOptions;

        // generate saved json object
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, serializerOptions);

        options.Logger?.Log(
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

        options.Logger?.Log(
            LogLevel.Trace,
            "JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );
        return bytes;
    }
}
