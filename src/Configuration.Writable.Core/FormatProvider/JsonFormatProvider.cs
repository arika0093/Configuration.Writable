using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
public class JsonFormatProvider : FormatProviderBase
{
    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } =
        new() { WriteIndented = false };

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "json";

    /// <inheritdoc />
    public override T LoadConfiguration<T>(WritableOptionsConfiguration<T> options)
    {
        var filePath = options.ConfigFilePath;
        if (!options.FileProvider.FileExists(filePath))
        {
            return new T();
        }

        var stream = options.FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return new T();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableOptionsConfiguration<T> options)
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
                    return new T();
                }
            }

            return JsonSerializer.Deserialize<T>(current.GetRawText(), JsonSerializerOptions)
                ?? new T();
        }

        return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonSerializerOptions) ?? new T();
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = GetSaveContents(config, options);
        await options
            .FileProvider.SaveToFileAsync(
                options.ConfigFilePath,
                contents,
                options.Logger,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the save contents for the configuration.
    /// </summary>
    private ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = JsonSerializerOptions.WriteIndented,
                Encoder = JsonSerializerOptions.Encoder,
            }
        );

        var sectionName = options.SectionName;
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            // No section name, serialize directly
            options.Logger?.Log(
                LogLevel.Trace,
                "Serializing configuration directly without section nesting"
            );
            JsonSerializer.Serialize(writer, config, JsonSerializerOptions);
        }
        else
        {
            options.Logger?.Log(
                LogLevel.Trace,
                "Creating nested section structure for section: {SectionName}",
                sectionName
            );

            // Split section name into parts
            var sections = GetSplitedSections(sectionName);

            // Write nested structure
            writer.WriteStartObject();
            WriteNestedSections(writer, sections, 0, config, JsonSerializerOptions);
            writer.WriteEndObject();
        }

        writer.Flush();
        var bytes = stream.ToArray();

        options.Logger?.Log(
            LogLevel.Trace,
            "JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );

        return bytes;
    }

    /// <summary>
    /// Recursively writes nested section structure using Utf8JsonWriter.
    /// </summary>
    private static void WriteNestedSections<T>(
        Utf8JsonWriter writer,
        string[] sections,
        int currentIndex,
        T config,
        JsonSerializerOptions options
    )
        where T : class, new()
    {
        if (currentIndex >= sections.Length)
        {
            return;
        }

        var sectionName = sections[currentIndex];
        writer.WritePropertyName(sectionName);

        if (currentIndex == sections.Length - 1)
        {
            // Last section, write the actual configuration
            JsonSerializer.Serialize(writer, config, options);
        }
        else
        {
            // More sections to go, write nested object
            writer.WriteStartObject();
            WriteNestedSections(writer, sections, currentIndex + 1, config, options);
            writer.WriteEndObject();
        }
    }
}
