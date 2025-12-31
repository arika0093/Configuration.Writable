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
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    public override object LoadConfiguration(
        Type type,
        Stream stream,
        List<string> sectionNameParts
    )
    {
        var jsonDocument = JsonDocument.Parse(stream);
        var root = jsonDocument.RootElement;

        // Navigate to the section if specified
        if (sectionNameParts.Count > 0)
        {
            if (JsonWriterHelper.TryNavigateToSection(root, sectionNameParts, out var current))
            {
                return JsonSerializer.Deserialize(
                        current.GetRawText(),
                        type,
                        JsonSerializerOptions
                    ) ?? Activator.CreateInstance(type)!;
            }
            else
            {
                // Section not found, return default instance
                return Activator.CreateInstance(type)!;
            }
        }

        return JsonSerializer.Deserialize(root.GetRawText(), type, JsonSerializerOptions)
            ?? Activator.CreateInstance(type)!;
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

        // Create serialize action that captures JsonSerializerOptions
        var serializeAction = CreateSerializeAction<T>(JsonSerializerOptions);

        if (existingDocument != null)
        {
            using (existingDocument)
            {
                // Merge with existing document
                JsonWriterHelper.WritePartialUpdate(
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
            options.Logger?.Log(
                LogLevel.Trace,
                "Creating new nested section structure for section: {SectionName}",
                string.Join(":", sections)
            );

            writer.WriteStartObject();
            JsonWriterHelper.WriteNestedSections(writer, sections, 0, config, serializeAction);
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
    /// Creates a serialize action for the given JsonSerializerOptions.
    /// </summary>
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    private static JsonSerializeAction<T> CreateSerializeAction<T>(JsonSerializerOptions options)
        where T : class, new()
    {
        return (writer, value) => JsonSerializer.Serialize(writer, value, options);
    }
}
