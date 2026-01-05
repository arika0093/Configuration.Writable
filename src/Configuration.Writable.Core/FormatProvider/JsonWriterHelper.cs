using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Delegate for serializing a value to a Utf8JsonWriter.
/// </summary>
/// <typeparam name="T">The type of the value to serialize.</typeparam>
/// <param name="writer">The JSON writer to write to.</param>
/// <param name="value">The value to serialize.</param>
internal delegate void JsonSerializeAction<in T>(Utf8JsonWriter writer, T value);

/// <summary>
/// Helper class for shared JSON writing operations between JsonFormatProvider and JsonAotFormatProvider.
/// </summary>
internal static class JsonWriterHelper
{
    /// <summary>
    /// Navigates to a section in a JSON document.
    /// </summary>
    /// <param name="root">The root JSON element.</param>
    /// <param name="sectionNameParts">The parts of the section path.</param>
    /// <param name="result">The resulting element if found.</param>
    /// <returns>True if the section was found, false otherwise.</returns>
    public static bool TryNavigateToSection(
        JsonElement root,
        List<string> sectionNameParts,
        out JsonElement result
    )
    {
        result = root;

        foreach (var section in sectionNameParts)
        {
            if (result.TryGetProperty(section, out var element))
            {
                result = element;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets save contents for full file write (no section name).
    /// </summary>
    /// <typeparam name="T">The type of the configuration.</typeparam>
    /// <param name="config">The configuration object to serialize.</param>
    /// <param name="writerOptions">The JSON writer options.</param>
    /// <param name="serializeAction">The action to serialize the config object.</param>
    /// <param name="logger">Optional logger for trace output.</param>
    /// <returns>The serialized bytes.</returns>
    public static ReadOnlyMemory<byte> GetFullSaveContents<T>(
        T config,
        JsonWriterOptions writerOptions,
        JsonSerializeAction<T> serializeAction,
        ILogger? logger
    )
        where T : class, new()
    {
        logger?.Log(LogLevel.Trace, "Serializing configuration directly without section nesting");

        // Use ArrayBufferWriter for better memory efficiency
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter, writerOptions);

        serializeAction(writer, config);
        writer.Flush();

        var bytes = bufferWriter.WrittenMemory.ToArray();

        logger?.Log(
            LogLevel.Trace,
            "JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );

        return bytes;
    }

    /// <summary>
    /// Gets save contents for partial write (with section name).
    /// </summary>
    /// <typeparam name="T">The type of the configuration.</typeparam>
    /// <param name="config">The configuration object to serialize.</param>
    /// <param name="sections">The section path parts.</param>
    /// <param name="writerOptions">The JSON writer options.</param>
    /// <param name="serializeAction">The action to serialize the config object.</param>
    /// <param name="fileProvider">The file provider to read existing file.</param>
    /// <param name="configFilePath">The configuration file path.</param>
    /// <param name="logger">Optional logger for trace output.</param>
    /// <returns>The serialized bytes.</returns>
    public static ReadOnlyMemory<byte> GetPartialSaveContents<T>(
        T config,
        List<string> sections,
        JsonWriterOptions writerOptions,
        JsonSerializeAction<T> serializeAction,
        IFileProvider fileProvider,
        string configFilePath,
        ILogger? logger
    )
        where T : class, new()
    {
        JsonDocument? existingDocument = null;

        // Try to read existing file using PipeReader
        if (fileProvider.FileExists(configFilePath))
        {
            try
            {
                var pipeReader = fileProvider.GetFilePipeReader(configFilePath);
                if (pipeReader != null)
                {
                    using var stream = pipeReader.AsStream(leaveOpen: false);
                    existingDocument = JsonDocument.Parse(stream);
                    logger?.Log(LogLevel.Trace, "Loaded existing JSON file for partial update");
                }
            }
            catch (JsonException ex)
            {
                logger?.Log(
                    LogLevel.Warning,
                    ex,
                    "Failed to parse existing JSON file, will create new file structure"
                );
            }
        }

        // Use ArrayBufferWriter for better memory efficiency
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter, writerOptions);

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
                    serializeAction
                );
            }
        }
        else
        {
            // No existing file, create new nested structure
            logger?.Log(
                LogLevel.Trace,
                "Creating new nested section structure for section: {SectionName}",
                string.Join(":", sections)
            );

            writer.WriteStartObject();
            WriteNestedSections(writer, sections, 0, config, serializeAction);
            writer.WriteEndObject();
        }

        writer.Flush();
        var bytes = bufferWriter.WrittenMemory.ToArray();

        logger?.Log(
            LogLevel.Trace,
            "Partial JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );

        return bytes;
    }

    /// <summary>
    /// Writes a partial update by merging existing JSON with new configuration at the specified section path.
    /// </summary>
    /// <typeparam name="T">The type of the configuration to serialize.</typeparam>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="existingElement">The existing JSON element to merge with.</param>
    /// <param name="sections">The section path parts.</param>
    /// <param name="currentIndex">The current index in the section path.</param>
    /// <param name="config">The configuration object to serialize.</param>
    /// <param name="serializeAction">The action to serialize the config object.</param>
    public static void WritePartialUpdate<T>(
        Utf8JsonWriter writer,
        JsonElement existingElement,
        List<string> sections,
        int currentIndex,
        T config,
        JsonSerializeAction<T> serializeAction
    )
        where T : class, new()
    {
        if (currentIndex >= sections.Count)
        {
            // Reached the target section depth, write the config
            serializeAction(writer, config);
            return;
        }

        var targetSection = sections[currentIndex];

        if (existingElement.ValueKind != JsonValueKind.Object)
        {
            // Existing element is not an object, replace with new structure
            writer.WriteStartObject();
            WriteNestedSections(writer, sections, currentIndex, config, serializeAction);
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
                    serializeAction(writer, config);
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
                        serializeAction
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
            WriteNestedSections(writer, sections, currentIndex, config, serializeAction);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Recursively writes nested section structure using Utf8JsonWriter.
    /// </summary>
    /// <typeparam name="T">The type of the configuration to serialize.</typeparam>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="sections">The section path parts.</param>
    /// <param name="currentIndex">The current index in the section path.</param>
    /// <param name="config">The configuration object to serialize.</param>
    /// <param name="serializeAction">The action to serialize the config object.</param>
    public static void WriteNestedSections<T>(
        Utf8JsonWriter writer,
        List<string> sections,
        int currentIndex,
        T config,
        JsonSerializeAction<T> serializeAction
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
            serializeAction(writer, config);
        }
        else
        {
            // More sections to go, write nested object
            writer.WriteStartObject();
            WriteNestedSections(writer, sections, currentIndex + 1, config, serializeAction);
            writer.WriteEndObject();
        }
    }
}
