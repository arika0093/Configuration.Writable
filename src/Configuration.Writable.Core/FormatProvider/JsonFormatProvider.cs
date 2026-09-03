using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FormatProvider;

#pragma warning disable CS0618 // IHasVersion remains supported for backward compatibility.
/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
public class JsonFormatProvider : FormatProviderBase, IOptionsSchemaMetadataProvider
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
    public OptionsSchemaMetadata? ReadSchemaMetadata(IWritableOptionsConfiguration options)
    {
        var filePath = options.ConfigFilePath;
        var pipeReader = options.FileProvider.GetFilePipeReader(filePath);
        if (pipeReader == null)
        {
            return null;
        }

        try
        {
            using var stream = pipeReader.AsStream(leaveOpen: false);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (
                !JsonWriterHelper.TryNavigateToSection(
                    root,
                    options.SectionNameParts,
                    out var current
                )
            )
            {
                return null;
            }

            if (current.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(
                    "Options schema metadata must be stored in a JSON object."
                );
            }

            var modelId = ReadOptionalString(current, OptionsSchemaMetadata.ModelIdPropertyName);
            var version = ReadOptionalVersion(current) ?? 1;
            return new OptionsSchemaMetadata(modelId, version);
        }
        finally
        {
            if (pipeReader is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!TryGetMetadataProperty(element, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"JSON metadata property '{propertyName}' must be a string.");
        }

        return value.GetString();
    }

    private int? ReadOptionalVersion(JsonElement element)
    {
        if (
            !TryGetMetadataProperty(
                element,
                OptionsSchemaMetadata.VersionPropertyName,
                out var value
            )
        )
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var version))
        {
            throw new FormatException("JSON metadata property 'Version' must be an integer.");
        }

        return version;
    }

    private bool TryGetMetadataProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value
    )
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var convertedName = JsonSerializerOptions.PropertyNamingPolicy?.ConvertName(propertyName);
        return convertedName is not null
            && convertedName != propertyName
            && element.TryGetProperty(convertedName, out value);
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
        // The stream owns the PipeReader when leaveOpen is false
        using var stream = reader.AsStream(leaveOpen: false);
        if (sectionNameParts.Count == 0)
        {
            return await JsonSerializer
                    .DeserializeAsync(stream, type, JsonSerializerOptions, cancellationToken)
                    .ConfigureAwait(false)
                ?? CreateDefault(type);
        }

        using var jsonDocument = await JsonDocument
            .ParseAsync(stream, default, cancellationToken)
            .ConfigureAwait(false);
        if (
            !JsonWriterHelper.TryNavigateToSection(
                jsonDocument.RootElement,
                sectionNameParts,
                out var current
            )
        )
        {
            return CreateDefault(type);
        }

        return JsonSerializer.Deserialize(current.GetRawText(), type, JsonSerializerOptions)
            ?? CreateDefault(type);
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
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
    private ReadOnlyMemory<byte> GetSaveContents<T>(T config, IWritableOptionsConfiguration options)
        where T : class, new()
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        var sections = options.SectionNameParts;
        var serializeAction = JsonWriterHelper.AddSchemaMetadata(
            CreateSerializeAction<T>(JsonSerializerOptions),
            options.SchemaMetadata,
            config is not IHasVersion
        );
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
