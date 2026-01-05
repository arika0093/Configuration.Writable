using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
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
                return JsonSerializer.Deserialize(current.GetRawText(), type, JsonSerializerOptions)
                    ?? Activator.CreateInstance(type)!;
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
#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = AotJsonReason)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJsonReason)]
#endif
    public override async ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
    {
        // Use JsonDocument.ParseAsync for efficient pipeline-based parsing
        var jsonDocument = await JsonDocument.ParseAsync(
                PipeReaderAsStream(reader),
                default,
                cancellationToken
            )
            .ConfigureAwait(false);
        var root = jsonDocument.RootElement;

        // Navigate to the section if specified
        if (sectionNameParts.Count > 0)
        {
            if (JsonWriterHelper.TryNavigateToSection(root, sectionNameParts, out var current))
            {
                return JsonSerializer.Deserialize(current.GetRawText(), type, JsonSerializerOptions)
                    ?? Activator.CreateInstance(type)!;
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

    /// <summary>
    /// Wraps a PipeReader as a Stream for compatibility with JsonDocument.ParseAsync
    /// </summary>
    private static Stream PipeReaderAsStream(PipeReader reader)
    {
        return reader.AsStream(leaveOpen: true);
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
        var serializeAction = CreateSerializeAction<T>(JsonSerializerOptions);
        var writerOptions = new JsonWriterOptions
        {
            Indented = JsonSerializerOptions.WriteIndented,
            Encoder = JsonSerializerOptions.Encoder,
        };

        if (sections.Count == 0)
        {
            return JsonWriterHelper.GetFullSaveContents(
                config,
                writerOptions,
                serializeAction,
                options.Logger
            );
        }
        else
        {
            options.Logger?.Log(
                LogLevel.Trace,
                "Using partial write for section: {SectionName}",
                string.Join(":", sections)
            );

            return JsonWriterHelper.GetPartialSaveContents(
                config,
                sections,
                writerOptions,
                serializeAction,
                options.FileProvider,
                options.ConfigFilePath,
                options.Logger
            );
        }
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
