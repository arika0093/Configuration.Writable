using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
public class JsonFormatProvider : FormatProviderBase
{
#if NET
    private const string AotJsonReason =
        "JsonSerializerOptions.TypeInfoResolver handles NativeAOT scenarios";
#endif

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
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    public override T LoadConfiguration<T>(Stream stream, WritableOptionsConfiguration<T> options)
    {
        var jsonDocument = JsonDocument.Parse(stream);
        var root = jsonDocument.RootElement;

        // Navigate to the section if specified
        var sections = options.SectionNameParts;
        if (sections.Count > 0)
        {
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

            // Use migration-aware deserialization
            return LoadConfigurationWithMigration(
                current,
                options,
                jsonElement =>
                {
                    if (jsonElement.TryGetProperty("Version", out var versionElement)
                        && versionElement.ValueKind == JsonValueKind.Number)
                    {
                        return versionElement.GetInt32();
                    }
                    return null;
                },
                (jsonElement, type) =>
                    JsonSerializer.Deserialize(jsonElement.GetRawText(), type, JsonSerializerOptions)
                        ?? Activator.CreateInstance(type)!
            );
        }

        // Use migration-aware deserialization
        return LoadConfigurationWithMigration(
            root,
            options,
            jsonElement =>
            {
                if (jsonElement.TryGetProperty("Version", out var versionElement)
                    && versionElement.ValueKind == JsonValueKind.Number)
                {
                    return versionElement.GetInt32();
                }
                return null;
            },
            (jsonElement, type) =>
                JsonSerializer.Deserialize(jsonElement.GetRawText(), type, JsonSerializerOptions)
                    ?? Activator.CreateInstance(type)!
        );
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
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
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

        var sections = options.SectionNameParts;
        if (sections.Count == 0)
        {
            // No section name, serialize directly (full file overwrite)
            options.Logger?.Log(
                LogLevel.Trace,
                "Serializing configuration directly without section nesting"
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

            JsonSerializer.Serialize(writer, config, JsonSerializerOptions);
            writer.Flush();
            var bytes = stream.ToArray();

            options.Logger?.Log(
                LogLevel.Trace,
                "JSON serialization completed successfully, size: {Size} bytes",
                bytes.Length
            );

            return bytes;
        }
        else
        {
            // Section specified - use partial write (merge with existing file)
            options.Logger?.Log(
                LogLevel.Trace,
                "Using partial write for section: {SectionName}",
                string.Join(":", sections)
            );

            return GetPartialSaveContents(config, options);
        }
    }

    /// <summary>
    /// Gets the save contents for partial write (when SectionName is specified).
    /// Reads existing file and merges the new configuration into the specified section.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private ReadOnlyMemory<byte> GetPartialSaveContents<T>(
        T config,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var sections = options.SectionNameParts;
        JsonDocument? existingDocument = null;

        // Try to read existing file
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            try
            {
                using var fileStream = options.FileProvider.GetFileStream(options.ConfigFilePath);
                if (fileStream != null && fileStream.Length > 0)
                {
                    existingDocument = JsonDocument.Parse(fileStream);
                    options.Logger?.Log(
                        LogLevel.Trace,
                        "Loaded existing JSON file for partial update"
                    );
                }
            }
            catch (JsonException ex)
            {
                options.Logger?.Log(
                    LogLevel.Warning,
                    ex,
                    "Failed to parse existing JSON file, will create new file structure"
                );
            }
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = JsonSerializerOptions.WriteIndented,
                Encoder = JsonSerializerOptions.Encoder,
            }
        );

        if (existingDocument != null)
        {
            using (existingDocument)
            {
                // Merge with existing document
                WritePartialUpdate(
                    writer,
                    existingDocument.RootElement,
                    sections,
                    0,
                    config,
                    JsonSerializerOptions
                );
            }
        }
        else
        {
            // No existing file, create new nested structure
            options.Logger?.Log(
                LogLevel.Trace,
                "Creating new nested section structure for section: {SectionName}",
                string.Join(":", sections)
            );

            writer.WriteStartObject();
            WriteNestedSections(writer, sections, 0, config, JsonSerializerOptions);
            writer.WriteEndObject();
        }

        writer.Flush();
        var bytes = stream.ToArray();

        options.Logger?.Log(
            LogLevel.Trace,
            "Partial JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );

        return bytes;
    }

    /// <summary>
    /// Writes a partial update by merging existing JSON with new configuration at the specified section path.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private static void WritePartialUpdate<T>(
        Utf8JsonWriter writer,
        JsonElement existingElement,
        List<string> sections,
        int currentIndex,
        T config,
        JsonSerializerOptions options
    )
        where T : class, new()
    {
        if (currentIndex >= sections.Count)
        {
            // Reached the target section depth, write the config
            JsonSerializer.Serialize(writer, config, options);
            return;
        }

        var targetSection = sections[currentIndex];

        if (existingElement.ValueKind != JsonValueKind.Object)
        {
            // Existing element is not an object, replace with new structure
            writer.WriteStartObject();
            WriteNestedSections(writer, sections, currentIndex, config, options);
            writer.WriteEndObject();
            return;
        }

        // Write object and merge properties
        writer.WriteStartObject();

        foreach (var property in existingElement.EnumerateObject())
        {
            if (property.Name == targetSection)
            {
                // This is the target section, recurse or replace
                writer.WritePropertyName(property.Name);

                if (currentIndex == sections.Count - 1)
                {
                    // This is the final section, replace with new config
                    JsonSerializer.Serialize(writer, config, options);
                }
                else
                {
                    // More sections to go, recurse
                    WritePartialUpdate(
                        writer,
                        property.Value,
                        sections,
                        currentIndex + 1,
                        config,
                        options
                    );
                }
            }
            else
            {
                // Copy other properties as-is
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }

        // If target section doesn't exist in existing document, add it
        if (!existingElement.TryGetProperty(targetSection, out _))
        {
            WriteNestedSections(writer, sections, currentIndex, config, options);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Recursively writes nested section structure using Utf8JsonWriter.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private static void WriteNestedSections<T>(
        Utf8JsonWriter writer,
        List<string> sections,
        int currentIndex,
        T config,
        JsonSerializerOptions options
    )
        where T : class, new()
    {
        if (currentIndex >= sections.Count)
        {
            return;
        }

        var sectionName = sections[currentIndex];
        writer.WritePropertyName(sectionName);

        if (currentIndex == sections.Count - 1)
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
